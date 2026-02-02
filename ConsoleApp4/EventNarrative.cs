using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public static class EventNarrative
    {
        public static string GetDirection(EventMetrics m)
        {
            if (m.ReturnEvent <= -0.05m && m.ReturnPost <= -0.03m)
                return "Strong Bearish";
            if (m.ReturnEvent <= -0.03m)
                return "Bearish";
            if (m.ReturnEvent >= 0.05m && m.ReturnPost > 0)
                return "Strong Bullish";
            if (m.ReturnEvent >= 0.03m)
                return "Bullish";
            return "Neutral";
        }

        public static string GetReaction(EventMetrics m)
        {
            if (Math.Abs(m.ReturnPre) < 0.01m && Math.Abs(m.ReturnEvent) > 0.03m)
                return "Event-Driven Shock";

            if (Math.Abs(m.ReturnEvent) > 0.03m &&
                Math.Abs(m.ReturnPost) > Math.Abs(m.ReturnEvent))
                return "Event-Triggered Continuation";

            if (m.ReturnEvent * m.ReturnPost < 0)
                return "Event Reversal";

            if (m.RangeEvent > 1.5m * m.RangePre)
                return "Volatility Expansion";

            return "Low Impact";
        }

        public static string GetSeverity(EventMetrics m)
        {
            if (m.MaxDDPost <= -0.10m) return "Extreme";
            if (m.MaxDDPost <= -0.07m) return "Severe";
            if (m.MaxDDPost <= -0.04m) return "Moderate";
            return "Minor";
        }

        public static string GetSummary(EventMetrics m)
        {
            return $"{GetDirection(m)} | {GetReaction(m)} | {GetSeverity(m)}";
        }
    }

}
