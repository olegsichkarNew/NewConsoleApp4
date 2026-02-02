using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public sealed record VolatilityEvent(
    DateTime StartUtc,
    DateTime EndUtc,
    int WindowMinutes,

    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,

    decimal RangePct,
    decimal VolumeBtc
);

    public static class VolatilityEventAnalyse
    {
        public static void ExportVolatilityCsv(string path, IReadOnlyList<VolatilityEvent> events)
        {
            using var sw = new StreamWriter(path);
            sw.WriteLine("StartUtc,EndUtc,WindowMinutes,RangePct,VolumeBtc,Open,High,Low,Close");

            foreach (var e in events)
            {
                sw.WriteLine($"{e.StartUtc:O},{e.EndUtc:O},{e.WindowMinutes}," +
                             $"{e.RangePct},{e.VolumeBtc},{e.Open},{e.High},{e.Low},{e.Close}");
            }
        }

        public static List<VolatilityEvent> FindHighVolatilityWindows(
    IReadOnlyList<Candle> candles,
    int windowMinutes,
    decimal thresholdPct,
    int stepMinutes = 1)
        {
            var result = new List<VolatilityEvent>();
            if (candles.Count == 0) return result;

            int windowSize = windowMinutes; // т.к. 1 candle = 1 минута
            int step = Math.Max(1, stepMinutes);

            for (int i = 0; i + windowSize < candles.Count; i += step)
            {
                var window = candles.Skip(i).Take(windowSize).ToList();

                var open = window.First().Open;
                var close = window.Last().Close;
                var high = window.Max(c => c.High);
                var low = window.Min(c => c.Low);
                var vol = window.Sum(c => c.Volume);

                if (open <= 0) continue;

                var rangePct = (high - low) / open * 100m;

                if (rangePct >= thresholdPct)
                {
                    result.Add(new VolatilityEvent(
                        StartUtc: window.First().TimeUtc,
                        EndUtc: window.Last().TimeUtc,
                        WindowMinutes: windowMinutes,
                        Open: open,
                        High: high,
                        Low: low,
                        Close: close,
                        RangePct: rangePct,
                        VolumeBtc: vol
                    ));
                }
            }

            return result;
        }
        public static List<VolatilityEvent> MergeOverlapping(
            IReadOnlyList<VolatilityEvent> events,
            TimeSpan maxGap)
        {
            if (events.Count == 0) return new();

            var sorted = events.OrderBy(e => e.StartUtc).ToList();
            var merged = new List<VolatilityEvent>();

            var cur = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];

                if (next.StartUtc <= cur.EndUtc + maxGap)
                {
                    cur = cur with
                    {
                        EndUtc = next.EndUtc,
                        High = Math.Max(cur.High, next.High),
                        Low = Math.Min(cur.Low, next.Low),
                        RangePct = Math.Max(cur.RangePct, next.RangePct),
                        VolumeBtc = cur.VolumeBtc + next.VolumeBtc,
                        Close = next.Close
                    };
                }
                else
                {
                    merged.Add(cur);
                    cur = next;
                }
            }

            merged.Add(cur);
            return merged;
        }

    }
}
