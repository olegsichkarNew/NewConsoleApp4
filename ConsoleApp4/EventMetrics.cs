using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ConsoleApp4
{


    public sealed record EventMetrics(
        DateTime T0Utc,
        DateTime? EventStartUtc,
        DateTime? EventEndUtc,

        // Returns
        decimal ReturnPre,
        decimal ReturnEvent,
        decimal ReturnPost,

        // Drawdowns (negative numbers) / Runups (positive numbers)
        decimal MaxDDPre,
        decimal MaxDDEvent,
        decimal MaxDDPost,

        decimal MaxRUPre,
        decimal MaxRUEvent,
        decimal MaxRUPost,

        // Range volatility
        decimal RangePre,
        decimal RangeEvent,
        decimal RangePost,

        // Volume
        decimal VolPre,
        decimal VolEvent,
        decimal VolPost,
        decimal VolRatioPost
    );

    public static class EventStudy
    {
        /// <summary>
        /// Computes event-centered metrics for BTC candles:
        /// PRE   : [T0 - preWindow, T0)
        /// EVENT : [eventStart, eventEnd]   (if provided)
        /// POST  : (T0, T0 + postWindow]
        ///
        /// Candles MUST be sorted by TimeUtc ascending.
        /// </summary>
        public static EventMetrics ComputeEventMetrics(
            IReadOnlyList<Candle> candles,
            DateTime t0Utc,
            TimeSpan preWindow,
            TimeSpan postWindow,
            DateTime? eventStartUtc = null,
            DateTime? eventEndUtc = null)
        {
            if (candles == null || candles.Count == 0)
                throw new ArgumentException("candles is empty");

            t0Utc = EnsureUtc(t0Utc);
            if (eventStartUtc.HasValue) eventStartUtc = EnsureUtc(eventStartUtc.Value);
            if (eventEndUtc.HasValue) eventEndUtc = EnsureUtc(eventEndUtc.Value);

            // PRE slice: [T0-pre, T0)
            var preStart = t0Utc - preWindow;
            var preEnd = t0Utc;

            // POST slice: (T0, T0+post]
            var postStart = t0Utc;
            var postEnd = t0Utc + postWindow;

            var pre = Slice(candles, preStart, preEnd, includeStart: true, includeEnd: false);
            var post = Slice(candles, postStart, postEnd, includeStart: false, includeEnd: true);

            // EVENT slice (optional)
            List<Candle> ev = new();
            if (eventStartUtc.HasValue && eventEndUtc.HasValue && eventEndUtc.Value >= eventStartUtc.Value)
                ev = Slice(candles, eventStartUtc.Value, eventEndUtc.Value, includeStart: true, includeEnd: true);

            // For returns we need reference prices at boundaries.
            // Close(T0) is approximated as close of the last candle <= T0.
            var closeAtT0 = LastCloseAtOrBefore(candles, t0Utc);

            // Close at pre start (T0-pre) approximated as close at or before that moment
            var closeAtPreStart = LastCloseAtOrBefore(candles, preStart);

            // Close at post end (T0+post) approximated as close at or before postEnd
            var closeAtPostEnd = LastCloseAtOrBefore(candles, postEnd);

            decimal retPre = SafeReturn(closeAtPreStart, closeAtT0);
            decimal retPost = SafeReturn(closeAtT0, closeAtPostEnd);

            // EVENT return: (Close(end) - Open(start))/Open(start), based on candles inside event interval
            decimal retEvent = 0m;
            if (ev.Count >= 1)
            {
                var openEv = ev.First().Open;
                var closeEv = ev.Last().Close;
                retEvent = SafeReturn(openEv, closeEv);
            }

            // Drawdown/Runup relative to anchor (for PRE: anchor closeAtT0, for POST: anchor closeAtT0,
            // for EVENT: anchor open of event)
            var (ddPre, ruPre) = DrawdownRunup(pre, anchorPrice: closeAtT0);
            var (ddPost, ruPost) = DrawdownRunup(post, anchorPrice: closeAtT0);

            decimal ddEvent = 0m, ruEvent = 0m;
            if (ev.Count >= 1)
            {
                var anchor = ev.First().Open;
                (ddEvent, ruEvent) = DrawdownRunup(ev, anchor);
            }

            // Range volatility per segment
            decimal rangePre = RangePct(pre);
            decimal rangePost = RangePct(post);
            decimal rangeEvent = RangePct(ev);

            // Volumes
            decimal volPre = pre.Sum(c => c.Volume);
            decimal volPost = post.Sum(c => c.Volume);
            decimal volEvent = ev.Sum(c => c.Volume);

            decimal volRatioPost = (volPre > 0) ? (volPost / volPre) : 0m;

            return new EventMetrics(
                T0Utc: t0Utc,
                EventStartUtc: eventStartUtc,
                EventEndUtc: eventEndUtc,

                ReturnPre: retPre,
                ReturnEvent: retEvent,
                ReturnPost: retPost,

                MaxDDPre: ddPre,
                MaxDDEvent: ddEvent,
                MaxDDPost: ddPost,

                MaxRUPre: ruPre,
                MaxRUEvent: ruEvent,
                MaxRUPost: ruPost,

                RangePre: rangePre,
                RangeEvent: rangeEvent,
                RangePost: rangePost,

                VolPre: volPre,
                VolEvent: volEvent,
                VolPost: volPost,
                VolRatioPost: volRatioPost
            );
        }

        // ---------------- helpers ----------------

        private static DateTime EnsureUtc(DateTime dt)
            => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        /// <summary>
        /// Slice candles by time interval. Candles MUST be sorted ascending.
        /// Uses binary search to avoid O(N) scans per event.
        /// </summary>
        private static List<Candle> Slice(
            IReadOnlyList<Candle> candles,
            DateTime startUtc,
            DateTime endUtc,
            bool includeStart,
            bool includeEnd)
        {
            if (endUtc < startUtc) return new List<Candle>();

            int left = LowerBound(candles, startUtc, includeStart);
            int right = UpperBound(candles, endUtc, includeEnd);

            int count = right - left;
            if (count <= 0) return new List<Candle>();
            var res = new List<Candle>(count);
            for (int i = left; i < right; i++) res.Add(candles[i]);
            return res;
        }

        // First index where TimeUtc > start (if includeStart=false) OR >= start (if includeStart=true)
        private static int LowerBound(IReadOnlyList<Candle> candles, DateTime t, bool include)
        {
            int lo = 0, hi = candles.Count;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                var cmp = candles[mid].TimeUtc.CompareTo(t);
                if (cmp < 0 || (cmp == 0 && !include))
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        // First index where TimeUtc > end (if includeEnd=true) OR >= end (if includeEnd=false)
        private static int UpperBound(IReadOnlyList<Candle> candles, DateTime t, bool includeEnd)
        {
            int lo = 0, hi = candles.Count;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                var cmp = candles[mid].TimeUtc.CompareTo(t);
                if (cmp < 0 || (cmp == 0 && includeEnd))
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private static decimal LastCloseAtOrBefore(IReadOnlyList<Candle> candles, DateTime tUtc)
        {
            // find first index where TimeUtc > t, then step back
            int idx = UpperBound(candles, tUtc, includeEnd: true) - 1;
            if (idx < 0) idx = 0;
            return candles[idx].Close;
        }

        private static decimal SafeReturn(decimal fromPrice, decimal toPrice)
        {
            if (fromPrice <= 0) return 0m;
            return (toPrice - fromPrice) / fromPrice; // fraction, multiply by 100 if you want %
        }

        /// <summary>
        /// Max drawdown/runup in a segment relative to anchorPrice.
        /// Returns:
        ///  dd = min( (Low - anchor)/anchor )  (<=0)
        ///  ru = max( (High - anchor)/anchor ) (>=0)
        /// </summary>
        private static (decimal dd, decimal ru) DrawdownRunup(IReadOnlyList<Candle> seg, decimal anchorPrice)
        {
            if (seg == null || seg.Count == 0 || anchorPrice <= 0) return (0m, 0m);

            decimal minLow = seg.Min(c => c.Low);
            decimal maxHigh = seg.Max(c => c.High);

            decimal dd = (minLow - anchorPrice) / anchorPrice;   // negative or 0
            decimal ru = (maxHigh - anchorPrice) / anchorPrice;  // positive or 0
            return (dd, ru);
        }

        /// <summary>
        /// RangePct = (maxHigh - minLow) / openFirst
        /// Returns fraction (e.g. 0.15 for 15%).
        /// </summary>
        private static decimal RangePct(IReadOnlyList<Candle> seg)
        {
            if (seg == null || seg.Count == 0) return 0m;

            var open = seg.First().Open;
            if (open <= 0) return 0m;

            var maxHigh = seg.Max(c => c.High);
            var minLow = seg.Min(c => c.Low);

            return (maxHigh - minLow) / open;
        }
    }

}
