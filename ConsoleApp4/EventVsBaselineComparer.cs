using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    using System;
    using System.Globalization;

    public sealed record EventVsBaselineComparison
    {
        public string EventCode;

        // Differences (event - baseline)
        public decimal ShockShareDiff;
        public decimal HighStressShareDiff;
        public decimal MedianMaxDDPostDiff;
        public decimal MedianReturnPostDiff;
        public decimal MedianRangePostDiff;
        public decimal MedianVolRatioPostDiff;

        // Flags
        public bool HigherShockRisk;
        public bool HigherDrawdownRisk;
        public bool HigherVolatility;

        public string SummaryText;
    }

    public static class EventVsBaselineComparer
    {
        public static EventVsBaselineComparison Compare(
            EventClassSummary evt,
            EventClassSummary baseline)
        {
            if (evt.TotalOccurrences == 0 || baseline.TotalOccurrences == 0)
                throw new ArgumentException("Both event and baseline must have data.");

            decimal ShockShare(EventClassSummary s) =>
                s.ShockCount / (decimal)s.TotalOccurrences;

            decimal HighStressShare(EventClassSummary s) =>
                (s.HighVolCount + s.StressCount) / (decimal)s.TotalOccurrences;

            var shockDiff = ShockShare(evt) - ShockShare(baseline);
            var stressDiff = HighStressShare(evt) - HighStressShare(baseline);

            var ddDiff = evt.MedianMaxDDPost - baseline.MedianMaxDDPost;
            var retDiff = evt.MedianReturnPost - baseline.MedianReturnPost;
            var rangeDiff = evt.MedianRangePost - baseline.MedianRangePost;
            var volRatioDiff = evt.MedianVolRatioPost - baseline.MedianVolRatioPost;

            bool higherShock = shockDiff > 0.10m;
            bool higherDD = ddDiff < -0.02m;
            bool higherVol = stressDiff > 0.10m || rangeDiff > 0.02m;

            string summary = BuildSummary(
                evt.EventCode,
                shockDiff,
                stressDiff,
                ddDiff,
                retDiff,
                rangeDiff,
                volRatioDiff,
                higherShock,
                higherDD,
                higherVol
            );

            return new EventVsBaselineComparison
            {
                EventCode = evt.EventCode,

                ShockShareDiff = shockDiff,
                HighStressShareDiff = stressDiff,
                MedianMaxDDPostDiff = ddDiff,
                MedianReturnPostDiff = retDiff,
                MedianRangePostDiff = rangeDiff,
                MedianVolRatioPostDiff = volRatioDiff,

                HigherShockRisk = higherShock,
                HigherDrawdownRisk = higherDD,
                HigherVolatility = higherVol,

                SummaryText = summary
            };
        }

        private static string BuildSummary(
            string eventCode,
            decimal shockDiff,
            decimal stressDiff,
            decimal ddDiff,
            decimal retDiff,
            decimal rangeDiff,
            decimal volRatioDiff,
            bool higherShock,
            bool higherDD,
            bool higherVol)
        {
            string pct(decimal x) => (x * 100m).ToString("0.0", CultureInfo.InvariantCulture) + "%";

            var s = $"{eventCode}: ";

            if (!higherShock && !higherDD && !higherVol)
            {
                s += "Market behaviour around this event is statistically similar to random timestamps.";
                return s;
            }

            if (higherShock)
                s += $"higher incidence of event-driven shocks ({pct(shockDiff)} vs baseline). ";

            if (higherVol)
                s += $"elevated volatility regime frequency ({pct(stressDiff)} High/Stress share). ";

            if (higherDD)
                s += $"worse post-event drawdowns (Δ median MaxDD {pct(ddDiff)}). ";

            if (!higherShock && !higherVol && !higherDD)
                s += "no material deviation from baseline.";

            return s.Trim();
        }
    }

}
