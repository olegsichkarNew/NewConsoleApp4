using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public static class BaselineGenerator
    {
        public static List<DateTime> GenerateRandomT0(
            IReadOnlyList<Candle> candles,
            IReadOnlyList<(DateTime Start, DateTime End)> excludedWindows,
            int count,
            TimeSpan minSpacing,
            TimeSpan preWindow,
            TimeSpan postWindow,
            int seed = 42)
        {
            var rnd = new Random(seed);

            var minTime = candles.First().TimeUtc + preWindow;
            var maxTime = candles.Last().TimeUtc - postWindow;

            var accepted = new List<DateTime>();

            int guard = 0;
            while (accepted.Count < count && guard++ < count * 50)
            {
                var t0 = minTime + TimeSpan.FromSeconds(
                    rnd.NextDouble() * (maxTime - minTime).TotalSeconds);

                // 1) spacing
                if (accepted.Any(x => Math.Abs((x - t0).TotalMinutes) < minSpacing.TotalMinutes))
                    continue;

                // 2) exclude real events
                if (excludedWindows.Any(w =>
                    t0 >= w.Start - postWindow &&
                    t0 <= w.End + preWindow))
                    continue;

                accepted.Add(t0);
            }

            if (accepted.Count < count)
                throw new InvalidOperationException("Could not generate enough baseline T0s.");

            return accepted;
        }
    }

}
