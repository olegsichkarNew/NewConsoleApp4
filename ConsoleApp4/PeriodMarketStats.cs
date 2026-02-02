using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public sealed record PeriodMarketStats(
        int PeriodIndex,
        DateTime StartUtc,
        DateTime EndUtc,
        double DurationHours,

        decimal Open,
        decimal Close,
        decimal High,
        decimal Low,

        decimal ChangePct,
        decimal MaxUpPct,
        decimal MaxDownPct,

        decimal TotalVolume,
        decimal AvgVolume,
        int Candles
    );

    public static class MarketAnalyzer
    {
        public static PeriodMarketStats? AnalyzePeriod(int idx, DateTime startUtc, DateTime endUtc, List<Candle> candles)
        {
            // candles должны быть отсортированы по времени
            // Идеально: один раз отсортировать после загрузки.
            // Важно: интервал включительный - под твою логику Period
            var slice = candles.Where(c => c.TimeUtc >= startUtc && c.TimeUtc <= endUtc).ToList();
            if (slice.Count == 0) return null;

            var open = slice.First().Open;
            var close = slice.Last().Close;
            var high = slice.Max(x => x.High);
            var low = slice.Min(x => x.Low);

            decimal changePct = (close - open) / open * 100m;
            decimal maxUpPct = (high - open) / open * 100m;
            decimal maxDownPct = (low - open) / open * 100m;

            var totalVol = slice.Sum(x => x.Volume);
            var avgVol = totalVol / slice.Count;

            return new PeriodMarketStats(
                PeriodIndex: idx,
                StartUtc: startUtc,
                EndUtc: endUtc,
                DurationHours: (endUtc - startUtc).TotalHours,

                Open: open,
                Close: close,
                High: high,
                Low: low,

                ChangePct: changePct,
                MaxUpPct: maxUpPct,
                MaxDownPct: maxDownPct,

                TotalVolume: totalVol,
                AvgVolume: avgVol,
                Candles: slice.Count
            );
        }

        public static void ExportSummaryCsv(string path, IReadOnlyList<PeriodMarketStats> stats)
        {
            using var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            sw.WriteLine("PeriodIndex,StartUtc,EndUtc,DurationHours,Open,Close,High,Low,ChangePct,MaxUpPct,MaxDownPct,TotalVolume,AvgVolume,Candles");

            foreach (var s in stats)
            {
                sw.WriteLine(string.Join(",",
                    s.PeriodIndex,
                    s.StartUtc.ToString("O"),
                    s.EndUtc.ToString("O"),
                    s.DurationHours.ToString(CultureInfo.InvariantCulture),
                    s.Open.ToString(CultureInfo.InvariantCulture),
                    s.Close.ToString(CultureInfo.InvariantCulture),
                    s.High.ToString(CultureInfo.InvariantCulture),
                    s.Low.ToString(CultureInfo.InvariantCulture),
                    s.ChangePct.ToString(CultureInfo.InvariantCulture),
                    s.MaxUpPct.ToString(CultureInfo.InvariantCulture),
                    s.MaxDownPct.ToString(CultureInfo.InvariantCulture),
                    s.TotalVolume.ToString(CultureInfo.InvariantCulture),
                    s.AvgVolume.ToString(CultureInfo.InvariantCulture),
                    s.Candles
                ));
            }
        }
    }

}
