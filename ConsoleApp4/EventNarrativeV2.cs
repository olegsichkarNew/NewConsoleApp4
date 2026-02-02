using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public static class EventNarrativeV2
    {
        public static string GetMarketRegime(EventMetrics m)
        {
            if (m.RangePost >= 0.10m && m.VolRatioPost >= 1.5m)
                return "Stress";

            if (m.RangePost >= 0.06m && m.VolRatioPost >= 1.2m)
                return "High Volatility";

            if (m.RangePost >= 0.03m && m.VolRatioPost >= 1.0m)
                return "Elevated Volatility";

            return "Calm";
        }

        public static string GetReactionPattern(EventMetrics m)
        {
            if (Math.Abs(m.ReturnPre) < 0.01m && Math.Abs(m.ReturnEvent) >= 0.03m)
                return "Event-Driven Shock";

            if (Math.Abs(m.ReturnEvent) >= 0.03m &&
                Math.Abs(m.ReturnPost) >= Math.Abs(m.ReturnEvent))
                return "Event-Triggered Continuation";

            if (m.ReturnEvent * m.ReturnPost < 0 && Math.Abs(m.ReturnPost) >= 0.01m)
                return "Event Reversal";

            if (m.RangeEvent >= 1.5m * m.RangePre)
                return "Volatility Expansion";

            return "Low Impact";
        }

        public static string GetDirectionBias(EventMetrics m)
        {
            if (m.ReturnEvent <= -0.02m || m.ReturnPost <= -0.02m)
                return "Bearish Bias";

            if (m.ReturnEvent >= 0.02m || m.ReturnPost >= 0.02m)
                return "Bullish Bias";

            return "Direction Uncertain";
        }

        public static string GetSummary(EventMetrics m)
        {
            return $"{GetMarketRegime(m)} | {GetReactionPattern(m)} | {GetDirectionBias(m)}";
        }
    }

}
