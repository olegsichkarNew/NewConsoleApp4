using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    public sealed record EventClassSummary(
        string EventCode,
        int TotalOccurrences,

        // Regimes
        int CalmCount,
        int ElevatedCount,
        int HighVolCount,
        int StressCount,

        // Reaction patterns
        int LowImpactCount,
        int VolExpCount,
        int ShockCount,
        int ContinuationCount,
        int ReversalCount,

        // Direction bias
        int BullishCount,
        int BearishCount,
        int UncertainCount,

        // Numeric aggregates (post window is often most useful)
        decimal AvgReturnPost,
        decimal MedianReturnPost,
        decimal AvgMaxDDPost,
        decimal MedianMaxDDPost,
        decimal AvgRangePost,
        decimal MedianRangePost,
        decimal AvgVolRatioPost,
        decimal MedianVolRatioPost,

        string NarrativeSummary
    );

    public static class EventClassAnalytics
    {
        /// <summary>
        /// Computes an aggregated summary for a single event class (e.g., "Venus-Mercury conjunction")
        /// using per-occurrence metrics already computed by ComputeEventMetrics.
        ///
        /// The caller provides the event code (label) and the list of per-occurrence EventMetrics plus
        /// their v2 labels (MarketRegime, ReactionPattern, DirectionBias).
        /// </summary>
        public static EventClassSummary ComputeEventClassSummary(
            string eventCode,
            IReadOnlyList<(EventMetrics Metrics, string MarketRegime, string ReactionPattern, string DirectionBias)> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return new EventClassSummary(
                    EventCode: eventCode,
                    TotalOccurrences: 0,

                    CalmCount: 0, ElevatedCount: 0, HighVolCount: 0, StressCount: 0,
                    LowImpactCount: 0, VolExpCount: 0, ShockCount: 0, ContinuationCount: 0, ReversalCount: 0,
                    BullishCount: 0, BearishCount: 0, UncertainCount: 0,

                    AvgReturnPost: 0, MedianReturnPost: 0,
                    AvgMaxDDPost: 0, MedianMaxDDPost: 0,
                    AvgRangePost: 0, MedianRangePost: 0,
                    AvgVolRatioPost: 0, MedianVolRatioPost: 0,

                    NarrativeSummary: "No occurrences."
                );
            }

            int total = rows.Count;

            // -------- counts --------
            int calm = rows.Count(r => r.MarketRegime == "Calm");
            int elev = rows.Count(r => r.MarketRegime == "Elevated Volatility");
            int high = rows.Count(r => r.MarketRegime == "High Volatility");
            int stress = rows.Count(r => r.MarketRegime == "Stress");

            int lowImpact = rows.Count(r => r.ReactionPattern == "Low Impact");
            int volExp = rows.Count(r => r.ReactionPattern == "Volatility Expansion");
            int shock = rows.Count(r => r.ReactionPattern == "Event-Driven Shock");
            int cont = rows.Count(r => r.ReactionPattern == "Event-Triggered Continuation");
            int rev = rows.Count(r => r.ReactionPattern == "Event Reversal");

            int bull = rows.Count(r => r.DirectionBias == "Bullish Bias");
            int bear = rows.Count(r => r.DirectionBias == "Bearish Bias");
            int unc = rows.Count(r => r.DirectionBias == "Direction Uncertain");

            // -------- numeric aggregates (POST) --------
            // Note: returns and ranges are fractions (0.05 = 5%). DD is negative fraction.
            var retPost = rows.Select(r => r.Metrics.ReturnPost).ToList();
            var ddPost = rows.Select(r => r.Metrics.MaxDDPost).ToList();
            var rangePost = rows.Select(r => r.Metrics.RangePost).ToList();
            var volRatioPost = rows.Select(r => r.Metrics.VolRatioPost).ToList();

            decimal avgRetPost = Average(retPost);
            decimal medRetPost = Median(retPost);

            decimal avgDdPost = Average(ddPost);
            decimal medDdPost = Median(ddPost);

            decimal avgRangePost = Average(rangePost);
            decimal medRangePost = Median(rangePost);

            decimal avgVolRatio = Average(volRatioPost);
            decimal medVolRatio = Median(volRatioPost);

            // -------- narrative summary (compact, deterministic) --------
            // Identify dominant regime/pattern/direction by max count
            string domRegime = ArgMax(
                new Dictionary<string, int>
                {
                    ["Calm"] = calm,
                    ["Elevated Volatility"] = elev,
                    ["High Volatility"] = high,
                    ["Stress"] = stress
                });

            string domPattern = ArgMax(
                new Dictionary<string, int>
                {
                    ["Low Impact"] = lowImpact,
                    ["Volatility Expansion"] = volExp,
                    ["Event-Driven Shock"] = shock,
                    ["Event-Triggered Continuation"] = cont,
                    ["Event Reversal"] = rev
                });

            string domDir = ArgMax(
                new Dictionary<string, int>
                {
                    ["Bullish Bias"] = bull,
                    ["Bearish Bias"] = bear,
                    ["Direction Uncertain"] = unc
                });

            // Risk flags (simple heuristics)
            // Stress/HighVol share and typical drawdown/volatility
            decimal shareStressOrHigh = (high + stress) / (decimal)total;
            bool isVolatilityAmplifier = shareStressOrHigh >= 0.20m || (avgRangePost >= 0.06m) || (avgVolRatio >= 1.20m);

            bool bearishTail = Percentile(ddPost, 0.10m) <= -0.07m; // 10th percentile drawdown <= -7%
            bool bullishTail = Percentile(retPost, 0.90m) >= 0.07m; // 90th percentile post return >= +7%

            string narrative =
                $"{eventCode}: {total} occurrences. " +
                $"Dominant regime: {domRegime}. Dominant reaction: {domPattern}. Direction: {domDir}. " +
                $"Post-window medians: Return {ToPct(medRetPost)}, MaxDD {ToPct(medDdPost)}, Range {ToPct(medRangePost)}, VolRatio {medVolRatio:0.###}. " +
                $"{(isVolatilityAmplifier ? "Often coincides with volatility expansion / elevated activity." : "Typically low-impact in the post window.")} " +
                $"{(bearishTail ? "Bearish tail-risk present (deep drawdowns in worst cases)." : "")}" +
                $"{(bullishTail ? " Bullish tail upside present (strong rebounds in best cases)." : "")}";

            return new EventClassSummary(
                EventCode: eventCode,
                TotalOccurrences: total,

                CalmCount: calm,
                ElevatedCount: elev,
                HighVolCount: high,
                StressCount: stress,

                LowImpactCount: lowImpact,
                VolExpCount: volExp,
                ShockCount: shock,
                ContinuationCount: cont,
                ReversalCount: rev,

                BullishCount: bull,
                BearishCount: bear,
                UncertainCount: unc,

                AvgReturnPost: avgRetPost,
                MedianReturnPost: medRetPost,
                AvgMaxDDPost: avgDdPost,
                MedianMaxDDPost: medDdPost,
                AvgRangePost: avgRangePost,
                MedianRangePost: medRangePost,
                AvgVolRatioPost: avgVolRatio,
                MedianVolRatioPost: medVolRatio,

                NarrativeSummary: narrative
            );
        }

        // ---------------- helpers ----------------

        private static decimal Average(IReadOnlyList<decimal> xs)
            => xs.Count == 0 ? 0m : xs.Sum() / xs.Count;

        private static decimal Median(List<decimal> xs)
        {
            if (xs.Count == 0) return 0m;
            xs.Sort();
            int mid = xs.Count / 2;
            if (xs.Count % 2 == 1) return xs[mid];
            return (xs[mid - 1] + xs[mid]) / 2m;
        }

        private static decimal Percentile(List<decimal> xs, decimal p01)
        {
            if (xs.Count == 0) return 0m;
            if (p01 <= 0) return xs.Min();
            if (p01 >= 1) return xs.Max();

            xs.Sort();
            double pos = (xs.Count - 1) * (double)p01;
            int lo = (int)Math.Floor(pos);
            int hi = (int)Math.Ceiling(pos);
            if (lo == hi) return xs[lo];

            decimal w = (decimal)(pos - lo);
            return xs[lo] * (1 - w) + xs[hi] * w;
        }

        private static string ArgMax(Dictionary<string, int> counts)
            => counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;

        private static string ToPct(decimal x)
            => (x * 100m).ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

}
