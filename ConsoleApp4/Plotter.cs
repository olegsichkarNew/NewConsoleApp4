using ScottPlot;

namespace ConsoleApp4
{
    public static class Plotter
    {
        public static void PlotPriceWithPeriods(
            string pngPath,
            List<Candle> candles,
            IReadOnlyList<(DateTime StartUtc, DateTime EndUtc, bool IsUp)> periods,
            int width = 1600,
            int height = 900)
        {
            // x = OADate (ScottPlot любит double)
            double[] xs = candles.Select(c => c.TimeUtc.ToOADate()).ToArray();
            double[] ys = candles.Select(c => (double)c.Close).ToArray();

            var plt = new ScottPlot.Plot();
            plt.Add.SignalXY(xs, ys);

            // полосы периодов
            foreach (var p in periods)
            {
                double x1 = p.StartUtc.ToOADate();
                double x2 = p.EndUtc.ToOADate();

                //// В ScottPlot есть Add.VerticalSpan
                //var span = plt.Add.VerticalSpan(x1, x2);
                //// НЕ задаю конкретные цвета по твоим правилам? — тут это не matplotlib,
                //// но если хочешь строго "не задавать цвета", скажи, и я сделаю одним цветом.
                //span.Color = p.IsUp ? System.Drawing.Color.FromArgb(40, System.Drawing.Color.Green)
                //                    : System.Drawing.Color.FromArgb(40, System.Drawing.Color.Red);
            }

            plt.Axes.DateTimeTicksBottom();
            plt.Title("BTC Close with Astro Periods");
            plt.YLabel("Price");
            plt.XLabel("Time (UTC)");

            plt.SavePng(pngPath, width, height);
        }
    }
}
