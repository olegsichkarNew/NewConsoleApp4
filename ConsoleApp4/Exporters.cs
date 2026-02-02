using ConsoleApp4;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AstroSwissEph
{
    public static class Exporters
    {
            public static void ExportHitsWithMarketCsv(
        string path,
        IReadOnlyList<Period> periods,
        SearchRequest req,
        IReadOnlyList<Candle> btcCandles,
        TimeSpan marketBar,
        TimeSpan hitStep)
    {
        // Индексируем BTC по времени (округление до бара)
        var btcByTime = btcCandles
            .GroupBy(c => AlignToBar(c.TimeUtc, marketBar))
            .ToDictionary(g => g.Key, g => g.Last());

        var sb = new StringBuilder();

        // header
        sb.Append("PeriodIndex,HitIndex,MinutesFromPeriodStart,TimeUtc");
        foreach (var b in req.Bodies)
            sb.Append($",Lon_{b},Speed_{b},Sign_{b},DegInSign_{b}");

        sb.Append(",BtcOpen,BtcHigh,BtcLow,BtcClose,BtcVolume");
        sb.Append(",BtcRetPrevHit");             // close(t) vs close(t-hitStep)
        sb.Append(",BtcRetFromPeriodStart");     // close(t) vs close(period start)
        sb.AppendLine();

        // Для быстрого lookup "предыдущий hit" используем словарь по времени hit-ов
        // (можно и без него, но так проще и точнее)
        foreach (var (p, pIndex) in periods.Select((p, i) => (p, i)))
        {
            // сортируем samples внутри периода
            var samples = p.Samples.OrderBy(x => x.TimeUtc).ToList();

            // close на старте периода (по ближайшей свече)
            decimal? periodStartClose = TryGetBtcClose(btcByTime, AlignToBar(p.StartUtc, marketBar));

            for (int hIndex = 0; hIndex < samples.Count; hIndex++)
            {
                var h = samples[hIndex];
                var t = h.TimeUtc;

                sb.Append(pIndex).Append(',');
                sb.Append(hIndex).Append(',');

                var minsFromStart = (t - p.StartUtc).TotalMinutes;
                sb.Append(minsFromStart.ToString("0", CultureInfo.InvariantCulture)).Append(',');

                sb.Append(t.ToString("O"));

                foreach (var b in req.Bodies)
                {
                    var s = h.Bodies[b];
                    sb.Append($",{Helper.F4(s.LonDeg)}");
                    sb.Append($",{Helper.F4(s.SpeedDegPerDay)}");
                    sb.Append($",{s.ZodiacSign}");
                    sb.Append($",{Helper.F4(s.DegreeInSign)}");
                }

                var key = AlignToBar(t, marketBar);
                if (!btcByTime.TryGetValue(key, out var c))
                {
                    // no BTC data -> empty market cols
                    sb.Append(",,,,,"); // OHLCV
                    sb.Append(",,");    // returns
                    sb.AppendLine();
                    continue;
                }

                sb.Append($",{Helper.F2(c.Open)}");
                sb.Append($",{Helper.F2(c.High)}");
                sb.Append($",{Helper.F2(c.Low)}");
                sb.Append($",{Helper.F2(c.Close)}");
                sb.Append($",{Helper.F4(c.Volume)}");

                // Return vs previous hitStep
                var prevKey = AlignToBar(t - hitStep, marketBar);
                decimal retPrev = 0m;
                var prevClose = TryGetBtcClose(btcByTime, prevKey);
                if (prevClose.HasValue && prevClose.Value > 0)
                    retPrev = (c.Close - prevClose.Value) / prevClose.Value;

                // Return from period start
                decimal retFromStart = 0m;
                if (periodStartClose.HasValue && periodStartClose.Value > 0)
                    retFromStart = (c.Close - periodStartClose.Value) / periodStartClose.Value;

                sb.Append($",{Helper.F4(retPrev*100)}");
                sb.Append($",{Helper.F4(retFromStart * 100)}");

                sb.AppendLine();
            }
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static DateTime AlignToBar(DateTime utc, TimeSpan bar)
    {
        if (utc.Kind != DateTimeKind.Utc)
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        long ticks = utc.Ticks / bar.Ticks * bar.Ticks;
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static decimal? TryGetBtcClose(Dictionary<DateTime, Candle> btcByTime, DateTime key)
        => btcByTime.TryGetValue(key, out var c) ? c.Close : null;

        /// <summary>Exports periods to a compact CSV: StartUtc, EndUtc, DurationHours, Bodies, Mode, OrbDeg</summary>
        public static void ExportPeriodsCsv(string path, IReadOnlyList<Period> periods, SearchRequest req, string modeName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("StartUtc,EndUtc,DurationHours,Bodies,Mode,OrbDeg,Samples");

            foreach (var p in periods)
            {
                var durH = (p.EndUtc - p.StartUtc).TotalHours;
                var bodies = string.Join("+", req.Bodies);
                sb.AppendLine($"{p.StartUtc:O},{p.EndUtc:O},{durH.ToString("0.###", CultureInfo.InvariantCulture)},{bodies},{modeName},{req.DiffDeg.ToString(CultureInfo.InvariantCulture)},{p.Samples.Count}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>Exports detailed samples (each hit) to CSV for debugging.</summary>
        public static void ExportHitsCsv(string path, IReadOnlyList<Period> periods, SearchRequest req)
        {
            var sb = new StringBuilder();
            sb.Append("TimeUtc");
            foreach (var b in req.Bodies)
                sb.Append($",Lon_{b},Speed_{b},Sign_{b},DegInSign_{b}");
            sb.AppendLine();

            foreach (var p in periods)
            foreach (var h in p.Samples)
            {
                sb.Append(h.TimeUtc.ToString("O"));

                foreach (var b in req.Bodies)
                {
                    var s = h.Bodies[b];
                    sb.Append($",{Helper.F4(s.LonDeg)}");
                    sb.Append($",{Helper.F4(s.SpeedDegPerDay)}");
                    sb.Append($",{s.ZodiacSign}");
                    sb.Append($",{Helper.F4(s.DegreeInSign)}");
                }

                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}
