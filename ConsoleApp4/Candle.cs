using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public sealed record Candle(
        DateTime TimeUtc,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        decimal Volume
    );

    public static class CandleCsv
    {
        public static List<Candle> Load(string path)
        {
            var lines = File.ReadLines(path).Skip(1);

            var list = new List<Candle>(capacity: 1_000_000);
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l)) continue;

                var p = l.Split(',');
                // Подстрой индексы под свой CSV, если отличаются:
                // timestamp,open,high,low,close,volume

                var t = ParseTimestampUtc(p[0]);
                var open = decimal.Parse(p[3], CultureInfo.InvariantCulture);
                var high = decimal.Parse(p[4], CultureInfo.InvariantCulture);
                var low = decimal.Parse(p[5], CultureInfo.InvariantCulture);
                var close = decimal.Parse(p[6], CultureInfo.InvariantCulture);
                var vol = decimal.Parse(p[7], CultureInfo.InvariantCulture);

                list.Add(new Candle(DateTime.SpecifyKind(t, DateTimeKind.Utc), open, high, low, close, vol));
            }

            return list;
        }
        static DateTime ParseTimestampUtc(string s)
        {
            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts))
                throw new FormatException($"Invalid timestamp: {s}");

            // эвристика: секунды ~1e9, миллисекунды ~1e12
            if (ts > 10_000_000_000)
                return DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime;
            else
                return DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
        }
    }
}
