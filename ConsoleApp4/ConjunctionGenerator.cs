using AstroSwissEph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ConsoleApp4.AstroEvents;

namespace ConsoleApp4
{
    public static class ConjunctionGenerator
    {
        public static IEnumerable<AstroEvent> GenerateConjunctionHitsLikeCsv(
    AstroSwissEph.IEphemeris eph,
    IBtcProvider btc,                 // см. ниже (OHLCV)
    DateTime startUtc,
    DateTime endUtc,
    int periodIndex,
    IReadOnlyList<SweBody> bodies,    // тут будет [Mercury, Venus]
    double diffDeg,                   // 0.5
    bool requireSameSign,             // true
    int stepMinutes)                  // 30
        {
            if (bodies.Count < 2) throw new ArgumentException("Need at least 2 bodies.");

            int hitIndex = 0;

            double? periodStartClose = null;
            double? prevHitClose = null;

            for (var t = startUtc; t <= endUtc; t = t.AddMinutes(stepMinutes))
            {
                var states = eph.GetStates(t, bodies);

                // Для CSV примера у нас 2 тела: Mercury и Venus
                var aBody = bodies[0];
                var bBody = bodies[1];

                var a = states[aBody];
                var b = states[bBody];

                var lonA = AstroMath.Norm360(a.LonDeg);
                var lonB = AstroMath.Norm360(b.LonDeg);

                int signA = AstroMath.SignIndex(lonA);
                int signB = AstroMath.SignIndex(lonB);

                if (requireSameSign && signA != signB)
                    continue;

                // “орбис” по полной долготе 0..180
                double orb = AstroMath.AbsDelta(lonA, lonB);
                if (orb > diffDeg)
                    continue;

                // BTC OHLCV на этот момент
                var ohlcv = btc.GetOhlcUtc(t); // (open, high, low, close, volume)

                // если нет close — пропускаем (или сохраняй null-метрики)
                if (ohlcv.close is null)
                    continue;

                var close = ohlcv.close.Value;

                periodStartClose ??= (double)close;
                prevHitClose ??= (double)close; // для первого hit

                double retPrev = (((double)close / prevHitClose.Value) - 1.0) * 100.0;
                double retFromStart = (((double)close / periodStartClose.Value) - 1.0) * 100.0;

                var evt = new AstroEvent
                (
                    EventTypeCode: "CONJUNCTION",
                    ConditionCode: requireSameSign ? "longitude_orb_same_sign" : "longitude_orb",
                    PeriodIndex: periodIndex,
                    HitIndex: hitIndex++,
                    MinutesFromStart: (int)Math.Round((t - startUtc).TotalMinutes),
                    TimeUtc: DateTime.SpecifyKind(t, DateTimeKind.Utc),
                    Bodies: new[]
                    {
                new EventBodyState(aBody, lonA, a.SpeedDegPerDay, signA, AstroMath.DegInSign(lonA)),
                new EventBodyState(bBody, lonB, b.SpeedDegPerDay, signB, AstroMath.DegInSign(lonB))
                    },
                    Metric: orb,
                    MetaJson: null,
                    BtcOpen: (double)ohlcv.open,
                    BtcHigh: (double)ohlcv.high,
                    BtcLow: (double)ohlcv.low,
                    BtcClose: (double)ohlcv.close,
                    BtcVolume: ohlcv.volume,
                    BtcRetPrevHit: retPrev,
                    BtcRetFromPeriodStart: retFromStart
                )
                {
                    // если у тебя AstroEvent record без settable props — добавь поля в конструктор
                    BtcVolume = ohlcv.volume,
                    BtcRetPrevHit = retPrev,
                    BtcRetFromPeriodStart = retFromStart
                };

                yield return evt;

                prevHitClose = (double)close;
            }
        }

        public static IEnumerable<AstroEvent> Generate(
            IEphemeris eph,
            IBtcProvider btc,
            DateTime startUtc,
            DateTime endUtc,
            int periodIndex,
            ConjunctionSpec spec,
            int stepMinutes = 60)
        {
            int hitIndex = 0;

            // дедуп по составу + “типу” condition, чтобы не сыпать одно и то же каждый шаг
            HashSet<int>? prevKey = null;

            for (var t = startUtc; t <= endUtc; t = t.AddMinutes(stepMinutes))
            {
                var states = spec.BodiesUniverse
                    .Select(b =>
                    {
                        var (lon, spd) = eph.GetLonSpeedUtc(t, b);
                        lon = AstroMath.Norm360(lon);
                        return new EventBodyState(
                            b,
                            lon,
                            spd,
                            AstroMath.SignIndex(lon),
                            AstroMath.DegInSign(lon)
                        );
                    })
                    .ToList();

                ClusterMatch? match = spec.ConditionCode switch
                {
                    "longitude_span" => FindLongitudeSpanCluster(states, spec.MinBodies, spec.ToleranceDeg, requireSameSign: spec.RequireSameSign),
                    "same_degree_in_sign" => FindSameDegreeInSignCluster(states, spec.MinBodies, spec.ToleranceDeg),
                    _ => throw new ArgumentOutOfRangeException(nameof(spec.ConditionCode), $"Unknown condition: {spec.ConditionCode}")
                };

                if (match is null)
                {
                    prevKey = null;
                    continue;
                }

                var key = match.Bodies.Select(x => (int)x.Body).ToHashSet();
                if (prevKey != null && prevKey.SetEquals(key))
                    continue;
                prevKey = key;

                var (o, h, l, c, v) = btc.GetOhlcUtc(t);

                yield return new AstroEvent(
                    EventTypeCode: "CONJUNCTION",
                    ConditionCode: spec.ConditionCode,
                    PeriodIndex: periodIndex,
                    HitIndex: hitIndex++,
                    MinutesFromStart: (int)Math.Round((t - startUtc).TotalMinutes),
                    TimeUtc: DateTime.SpecifyKind(t, DateTimeKind.Utc),
                    Bodies: match.Bodies,
                    Metric: match.Metric,                 // например span
                    MetaJson: match.MetaJson,             // параметры + детали
                    BtcOpen: (double?)o, BtcHigh: (double?)h, BtcLow: (double?)l, BtcClose: (double?)c, 
                    BtcVolume: v
                );
            }
        }

        private sealed record ClusterMatch(IReadOnlyList<EventBodyState> Bodies, double Metric, string MetaJson);

        // --- Condition A: longitude span (optionally same sign) ---
        private static ClusterMatch? FindLongitudeSpanCluster(
            List<EventBodyState> states,
            int minBodies,
            double maxSpanDeg,
            bool requireSameSign)
        {
            IEnumerable<IGrouping<int, EventBodyState>> groups =
                requireSameSign
                    ? states.GroupBy(s => s.Sign)
                    : new[] { states.GroupBy(_ => 0).Single() };

            ClusterMatch? best = null;

            foreach (var g in groups)
            {
                var list = g.OrderBy(x => x.DegInSign).ToList(); // 0..30
                if (list.Count < minBodies) continue;

                var win = BestWindowBySpan(list, x => x.DegInSign, maxSpanDeg);
                if (win.Count < minBodies) continue;

                var span = win.Last().DegInSign - win.First().DegInSign;

                var meta = $$"""
{"requireSameSign":{{requireSameSign.ToString().ToLowerInvariant()}},
 "sign":{{(requireSameSign ? g.Key : -1)}},
 "maxSpanDeg":{{maxSpanDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
 "spanDeg":{{span.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}
""";

                if (best == null || win.Count > best.Bodies.Count)
                    best = new ClusterMatch(win, span, meta);
            }

            return best;
        }

        // --- Condition B: same degree in sign, signs can differ ---
        private static ClusterMatch? FindSameDegreeInSignCluster(
            List<EventBodyState> states,
            int minBodies,
            double tolDeg)
        {
            var list = states.OrderBy(x => x.DegInSign).ToList();
            if (list.Count < minBodies) return null;

            var win = BestWindowBySpan(list, x => x.DegInSign, tolDeg);
            if (win.Count < minBodies) return null;

            var span = win.Last().DegInSign - win.First().DegInSign;

            var meta = $$"""
{"tolDeg":{{tolDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
 "spanDeg":{{span.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}
""";

            return new ClusterMatch(win, span, meta);
        }

        // sliding window: max count with (key[j]-key[i] <= maxSpan)
        private static List<EventBodyState> BestWindowBySpan(
            List<EventBodyState> sorted,
            Func<EventBodyState, double> key,
            double maxSpan)
        {
            int bestI = 0, bestJ = -1;
            int j = 0;

            for (int i = 0; i < sorted.Count; i++)
            {
                if (j < i) j = i;
                while (j + 1 < sorted.Count && (key(sorted[j + 1]) - key(sorted[i])) <= maxSpan)
                    j++;

                if (j - i > bestJ - bestI)
                {
                    bestI = i;
                    bestJ = j;
                }
            }

            if (bestJ < bestI) return new();
            return sorted.GetRange(bestI, bestJ - bestI + 1);
        }

        public static double CircularSpanDeg(IEnumerable<double> values, double circle)
        {
            var arr = values.Select(v =>
            {
                var x = v % circle;
                if (x < 0) x += circle;
                return x;
            }).OrderBy(x => x).ToArray();

            if (arr.Length < 2) return 0;

            double maxGap = 0;
            for (int i = 1; i < arr.Length; i++)
                maxGap = Math.Max(maxGap, arr[i] - arr[i - 1]);

            maxGap = Math.Max(maxGap, circle - (arr[^1] - arr[0]));
            return circle - maxGap;
        }

        public static IEnumerable<AstroEvent> GenerateConjunctionHits(
    IEphemeris eph,
    IBtcProvider btc,
    DateTime startUtc,
    DateTime endUtc,
    int periodIndex,
    IReadOnlyList<SweBody> bodies,
    double diffDeg,
    bool requireSameSign,
    int stepMinutes)
        {
            if (bodies.Count < 2)
                throw new ArgumentException("At least 2 bodies required.");

            int hitIndex = 0;
            double? periodStartClose = null;
            double? prevHitClose = null;

            for (var t = startUtc; t <= endUtc; t = t.AddMinutes(stepMinutes))
            {
                var states = eph.GetStates(t, bodies);

                // долготy
                var lons = bodies.Select(b => AstroMath.Norm360(states[b].LonDeg)).ToArray();
                double span;
                // проверка знаков
                if (requireSameSign)
                {
                    int sign0 = AstroMath.SignIndex(lons[0]);
                    if (lons.Any(l => AstroMath.SignIndex(l) != sign0))
                        continue;
                 span = AstroMath.LongitudeSpanDeg(lons);
               }
                else
                {
                    // ✅ новый режим: "same degree in sign"
                    var degs = lons.Select(AstroMath.DegInSign).ToArray();
                    span = CircularSpanDeg(degs, circle: 30.0);
                }
                // SPAN
                if (span > diffDeg)
                    continue;

                // BTC
                var ohlcv = btc.GetOhlcUtc(t);
                if (ohlcv.close is null)
                    continue;

                periodStartClose ??= (double)ohlcv.close;
                prevHitClose ??= (double)ohlcv.close;

                double retPrev =
                    (((double)ohlcv.close.Value / prevHitClose.Value) - 1) * 100;

                double retFromStart =
                    (((double)ohlcv.close.Value / periodStartClose.Value) - 1) * 100;

                yield return new AstroEvent(
                    EventTypeCode: "CONJUNCTION",
                    ConditionCode: requireSameSign
                        ? "longitude_span_same_sign"
                        : "longitude_span",
                    PeriodIndex: periodIndex,
                    HitIndex: hitIndex++,
                    MinutesFromStart: (int)(t - startUtc).TotalMinutes,
                    TimeUtc: t,
                    Bodies: bodies.Select((b, i) =>
                        new EventBodyState(
                            b,
                            lons[i],
                            states[b].SpeedDegPerDay,
                            AstroMath.SignIndex(lons[i]),
                            AstroMath.DegInSign(lons[i])
                        )).ToList(),
                    Metric: span,
                    MetaJson: null,
                    BtcOpen: (double)ohlcv.open,
                    BtcHigh: (double)ohlcv.high,
                    BtcLow: (double)ohlcv.low,
                    BtcClose: (double)ohlcv.close,
                    BtcVolume: ohlcv.volume,
                    BtcRetPrevHit: retPrev,
                    BtcRetFromPeriodStart: retFromStart
                );

                prevHitClose = (double)ohlcv.close;
            }
        }

    }


}
