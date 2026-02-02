using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    using AstroSwissEph;
    using System.Globalization;
    using System.Text;

    public static class EventMetricsExporter
    {
        public static void ExportEventMetricsCsv(
            IReadOnlyList<Candle> candles,
            IReadOnlyList<Period> periods,
            string csvPath,
            TimeSpan preWindow,
            TimeSpan postWindow)
        {
            if (candles == null || candles.Count == 0)
                throw new ArgumentException("candles empty");
            if (periods == null || periods.Count == 0)
                throw new ArgumentException("periods empty");

            candles = candles.OrderBy(c => c.TimeUtc).ToList();

            var sb = new StringBuilder(16_384);
            sb.AppendLine(
                "EventIndex,EventStartUtc,EventEndUtc,T0Utc," +
                "ReturnPre,ReturnEvent,ReturnPost," +
                "MaxDDPre,MaxDDEvent,MaxDDPost," +
                "MaxRUPre,MaxRUEvent,MaxRUPost," +
                "RangePre,RangeEvent,RangePost," +
                "VolPre,VolEvent,VolPost,VolRatioPost," +
                "MarketRegime,ReactionPattern,DirectionBias,SummaryV2"
            );
            var rows = new List<(EventMetrics Metrics, string MarketRegime, string ReactionPattern, string DirectionBias)>();

            for (int i = 0; i < periods.Count; i++)
            {
                var p = periods[i];

                DateTime t0 =
                    p.Samples != null && p.Samples.Count > 0
                        ? p.Samples[p.Samples.Count / 2].TimeUtc
                        : new DateTime(
                            (p.StartUtc.Ticks + p.EndUtc.Ticks) / 2,
                            DateTimeKind.Utc);

                var m = EventStudy.ComputeEventMetrics(
                    candles,
                    t0Utc: t0,
                    preWindow: preWindow,
                    postWindow: postWindow,
                    eventStartUtc: p.StartUtc,
                    eventEndUtc: p.EndUtc);

                AppendLine(sb, i, p, m);
                string regime = EventNarrativeV2.GetMarketRegime(m);
                string react = EventNarrativeV2.GetReactionPattern(m);
                string dir = EventNarrativeV2.GetDirectionBias(m);
                rows.Add((m, regime, react, dir));
            }

            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
            var summary = EventClassAnalytics.ComputeEventClassSummary(
                eventCode: "EventA",
                rows: rows
            );

            Console.WriteLine(summary.NarrativeSummary);
        }

        private static void AppendLine(
            StringBuilder sb,
            int index,
            Period p,
            EventMetrics m)
        {
            static string F(decimal x) =>
                x.ToString("0.################", CultureInfo.InvariantCulture);

            static string T(DateTime? dt) =>
                dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "";

            var marketRegime = EventNarrativeV2.GetMarketRegime(m);
            var reaction = EventNarrativeV2.GetReactionPattern(m);
            var direction = EventNarrativeV2.GetDirectionBias(m);
            var summary = EventNarrativeV2.GetSummary(m);

            sb.Append(index).Append(',');
            sb.Append(T(p.StartUtc)).Append(',');
            sb.Append(T(p.EndUtc)).Append(',');
            sb.Append(T(m.T0Utc)).Append(',');

            // returns, DD, ranges
            sb.Append(Helper.F4(m.ReturnPre)).Append(',');
            sb.Append(Helper.F4(m.ReturnEvent)).Append(',');
            sb.Append(Helper.F4(m.ReturnPost)).Append(',');

            // drawdowns / runups
            sb.Append(Helper.F4(m.MaxDDPre)).Append(',');
            sb.Append(Helper.F4(m.MaxDDEvent)).Append(',');
            sb.Append(Helper.F4(m.MaxDDPost)).Append(',');

            // ranges
            sb.Append(Helper.F4(m.RangePre)).Append(',');
            sb.Append(Helper.F4(m.RangeEvent)).Append(',');
            sb.Append(Helper.F4(m.RangePost)).Append(',');

            // volumes
            sb.Append(Helper.F0(m.VolPre)).Append(',');
            sb.Append(Helper.F0(m.VolEvent)).Append(',');
            sb.Append(Helper.F0(m.VolPost)).Append(',');

            // ratio
            sb.Append(Helper.F3(m.VolRatioPost)).Append(',');


            sb.Append(marketRegime).Append(',');
            sb.Append(reaction).Append(',');
            sb.Append(direction).Append(',');
            sb.Append('"').Append(summary).Append('"');

            sb.AppendLine();
        }
    }

}
