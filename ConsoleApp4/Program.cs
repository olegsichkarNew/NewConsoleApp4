using AstroSwissEph;
using ScottPlot.Plottables;
using static ConsoleApp4.AstroEvents;

namespace ConsoleApp4
{
    internal class Program
    {
        static readonly (string Code, string Prefix, bool IncludeRetro)[] Bodies =
{
    ("SUN", "sun", false),
    ("MOON", "moon", false),
    ("MERCURY", "mercury", true),
    ("VENUS", "venus", true),
    ("MARS", "mars", true),
    ("JUPITER", "jupiter", true),
    ("SATURN", "saturn", true),
    ("URANUS", "uranus", true),
    ("NEPTUNE", "neptune", true),
    ("PLUTO", "pluto", true),
    ("MEAN_NODE", "mean_node", true),
    ("TRUE_NODE", "true_node", true),
    ("MEAN_APOG", "mean_apog", true),
    ("OSCU_APOG", "oscu_apog", true),
};
        static string BuildBodyPivotSql()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("WITH body_pivot AS (");
            sb.AppendLine("  SELECT");
            sb.AppendLine("    eb.event_id,");

            for (int i = 0; i < Bodies.Length; i++)
            {
                var (code, pfx, incRetro) = Bodies[i];

                sb.AppendLine($"    MAX(CASE WHEN b.code='{code}' THEN eb.lon END) AS {pfx}_lon,");
                sb.AppendLine($"    MAX(CASE WHEN b.code='{code}' THEN eb.speed END) AS {pfx}_speed,");
                sb.AppendLine($"    MAX(CASE WHEN b.code='{code}' THEN eb.sign END) AS {pfx}_sign,");
                sb.AppendLine($"    MAX(CASE WHEN b.code='{code}' THEN eb.deg_in_sign END) AS {pfx}_deg_in_sign,");
                if (incRetro)
                    sb.AppendLine($"    MAX(CASE WHEN b.code='{code}' THEN eb.is_retro END) AS {pfx}_is_retro,");
            }

            // убрать последнюю запятую проще всего заменой
            var sql = sb.ToString().TrimEnd();
            // аккуратно удалим последнюю запятую перед FROM
            sql = System.Text.RegularExpressions.Regex.Replace(sql, @",\s*\z", "");

            sql += @"
  FROM event_body eb
  JOIN body b ON b.body_id = eb.body_id
  GROUP BY eb.event_id
)
SELECT fe.*, bp.*
FROM fact_event fe
JOIN body_pivot bp ON bp.event_id = fe.event_id;
";
            return sql;
        }

        static void Main(string[] args)
        {

            var s = BuildBodyPivotSql();
            var eph = new SwissEphEphemeris(
                ephePath: @"C:\my\myPrj\ConsoleApp4\ConsoleApp4\bin\Debug\net9.0\se",
                flags: SwissEphNative.SEFLG_SWIEPH | SwissEphNative.SEFLG_SPEED | SwissEphNative.SEFLG_SIDEREAL
            );
            var startUtc = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endUtc = new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc);

            var diffDeg = 0.5;
            int step = 30;
            //var (astroPeriods, req) = GetPeriods(eph, bodies, diffDeg, step, startUtc, endUtc);

            //var candles = LoadCandlesForPeriods(
            //    dir: @"C:\my\myPrj\ConsoleApp4\ConsoleApp4\data",        // где лежат месяцы
            //    symbol: "BTC-USDT",
            //    interval: "1m",
            //    periods: astroPeriods
            //);
            //candles.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));
            //var btcProvider = new CandlesBtcProvider(candles);

            var allBodies = Enumerable.Range(0, 14).Select(i => (SweBody)i).ToArray(); // 0..13

            using var db = new AstroEventDbWriter(
                @"C:\my\myPrj\ConsoleApp4\ConsoleApp4\db\testDB.sqlite"
            );

            // ⚠️ Глобальный индекс, чтобы не перетирать в БД
            int globalPeriodIndex = 0;

            // (опционально) чтобы не перегружать, можно стартовать с 2..4 и потом расширить
            for (int k = 2; k <= 4; k++)
            {
                foreach (var combo in Combinations(allBodies, k))
                {
                    if (!IsValidCombo(combo))
                        continue;
                    foreach (bool requireSameSign in new[] { true, false })
                    {
                        // 1) condition
                        // true  -> ConjunctionLongitudeCondition (same sign)
                        // false -> SameDegreeInSignCondition (signs can differ)
                        IEventCondition condition =
                            requireSameSign
                                ? new ConjunctionLongitudeCondition()
                                : new SameDegreeInSignCondition(); // <- если так называется у тебя

                        // 2) тег для экспорта/логов
                        // например: "k03_2-3-5_same1"
                        var comboKey = string.Join("-", combo.Select(b => ((int)b).ToString()));
                        var exportTag = $"k{k:D2}_{comboKey}_same{(requireSameSign ? 1 : 0)}";

                        // 3) найти периоды для этой комбинации
                        var (astroPeriods, req) = GetPeriods(
                            eph: eph,
                            bodies: combo,
                            diffDeg: diffDeg,
                            step: step,
                            startUtc: startUtc,
                            endUtc: endUtc,
                            condition: condition,
                            exportTag: exportTag
                        );

                        if (astroPeriods.Count == 0)
                            continue;

                        // 4) Загрузить свечи для найденных периодов
                        // ⚠️ Это очень дорого в цикле. Но как минимальный рабочий вариант — оставим.
                        var candles = LoadCandlesForPeriods(
                            dir: @"C:\my\myPrj\ConsoleApp4\ConsoleApp4\data",
                            symbol: "BTC-USDT",
                            interval: "1m",
                            periods: astroPeriods
                        );
                        candles.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));
                        var btcProvider = new CandlesBtcProvider(candles);

                        // 5) сохранить hits по периодам
                        foreach (var p in astroPeriods)
                        {
                            var events = ConjunctionGenerator.GenerateConjunctionHits(
                                eph: eph,
                                btc: btcProvider,
                                startUtc: p.StartUtc,
                                endUtc: p.EndUtc,

                                // !!! ВАЖНО: уникальный periodIndex в БД
                                periodIndex: globalPeriodIndex,

                                bodies: combo,
                                diffDeg: diffDeg,
                                requireSameSign: requireSameSign,
                                stepMinutes: step
                            );

                            int saved = 0;
                            foreach (var evt in events)
                            {
                                try
                                {
                                    db.SaveEvent(evt);
                                    saved++;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"SaveEvent failed at hit={evt.HitIndex}, time={evt.TimeUtc:o}: {ex.Message}");
                                    throw;
                                }
                            }
                            Console.WriteLine($"Saved events in this period: {saved}");
                            db.Commit(); // ← граница логического блока
                            Console.WriteLine($"Committed combo={comboKey}, sameSign={requireSameSign}");
                            globalPeriodIndex++; // чтобы в БД не было пересечений
                        }

                        Console.WriteLine($"Done {exportTag}, periods={astroPeriods.Count}");
                    }
                }
            }

            //db.Commit();
            Console.WriteLine("Done.");



            //OtherFunctions(astroPeriods, req, candles);

            Console.WriteLine("Done.");
        }

        static bool IsValidCombo(IReadOnlyList<SweBody> combo)
        {
            // group 1: nodes
            bool hasMeanNode = combo.Contains((SweBody)10); // MeanNode
            bool hasTrueNode = combo.Contains((SweBody)11); // TrueNode
            if (hasMeanNode && hasTrueNode) return false;

            // group 2: apogees
            bool hasMeanApog = combo.Contains((SweBody)12); // MeanApog
            bool hasOscuApog = combo.Contains((SweBody)13); // OscuApog
            if (hasMeanApog && hasOscuApog) return false;

            return true;
        }

        private static void OtherFunctions(IReadOnlyList<Period> astroPeriods, SearchRequest req, List<Candle> candles)
        {
            Exporters.ExportHitsWithMarketCsv(
    path: @"C:\temp\conjunction_hits_with_btc.csv",
    periods: astroPeriods,
    req: req,
    btcCandles: candles,
    marketBar: TimeSpan.FromMinutes(1),      // если btcCandles 1m
    hitStep: req.Step                        // 30m у тебя в req
);

            EventMetricsExporter.ExportEventMetricsCsv(
                candles,
                astroPeriods,
                csvPath: @"C:\temp\astro_event_A_metrics.csv",
                preWindow: TimeSpan.FromHours(48),
                postWindow: TimeSpan.FromHours(48));

            // 3. Excluded windows (реальные события)
            var excluded = astroPeriods
                .Select(p => (p.StartUtc, p.EndUtc))
                .ToList();

            // 4. Генерация baseline T0
            var baselineT0s = BaselineGenerator.GenerateRandomT0(
                candles,
                excludedWindows: excluded,
                count: astroPeriods.Count,
                minSpacing: TimeSpan.FromHours(12),
                preWindow: TimeSpan.FromHours(48),
                postWindow: TimeSpan.FromHours(48)
            );
            TimeSpan pre = TimeSpan.FromDays(2);
            TimeSpan eventWindow = TimeSpan.FromHours(12);
            TimeSpan post = TimeSpan.FromDays(2);
            // 5. Baseline rows
            var baselineRows = baselineT0s.Select(t0 =>
            {
                var m = EventStudy.ComputeEventMetrics(
        candles,
        eventStartUtc: t0 - eventWindow / 2,
        eventEndUtc: t0 + eventWindow / 2,
        t0Utc: t0,
        preWindow: pre,
        postWindow: post
    );
                return (
                    Metrics: m,
                    MarketRegime: EventNarrativeV2.GetMarketRegime(m),
                    ReactionPattern: EventNarrativeV2.GetReactionPattern(m),
                    DirectionBias: EventNarrativeV2.GetDirectionBias(m)
                );
            }).ToList();

            var eventRows = astroPeriods.Select(p =>
            {
                DateTime t0 =
    p.Samples != null && p.Samples.Count > 0
        ? p.Samples[p.Samples.Count / 2].TimeUtc
        : new DateTime(
            (p.StartUtc.Ticks + p.EndUtc.Ticks) / 2,
            DateTimeKind.Utc);

                var m = EventStudy.ComputeEventMetrics(
                    candles,
                    t0Utc: t0,
                    preWindow: pre,
                    postWindow: post,
                    eventStartUtc: p.StartUtc,
                    eventEndUtc: p.EndUtc);

                return (
                    Metrics: m,
                    MarketRegime: EventNarrativeV2.GetMarketRegime(m),
                    ReactionPattern: EventNarrativeV2.GetReactionPattern(m),
                    DirectionBias: EventNarrativeV2.GetDirectionBias(m)
                );
            }).ToList();

            var eventSummary =
       EventClassAnalytics.ComputeEventClassSummary("ASTRO_EVENT_A", eventRows);

            // 6. Summary baseline
            var baselineSummary =
                EventClassAnalytics.ComputeEventClassSummary("BASELINE_RANDOM_T0", baselineRows);
            var comparison =
    EventVsBaselineComparer.Compare(eventSummary, baselineSummary);

            Console.WriteLine(comparison.SummaryText);

            var raw = VolatilityEventAnalyse.FindHighVolatilityWindows(
    candles,
    windowMinutes: 180,
    thresholdPct: 15.0m,
    stepMinutes: 1);

            //var events = VolatilityEventAnalyse.MergeOverlapping(raw, TimeSpan.FromMinutes(5));

            //Console.WriteLine($"Найдено {events.Count} волатильных импульсов");
            //VolatilityEventAnalyse.ExportVolatilityCsv(@"C:\temp\Volatility.csv", events);
            //// Здесь пример: преобразуем в (Start, End, IsUp) по анализу
            //var stats = new List<PeriodMarketStats>();
            //for (int i = 0; i < astroPeriods.Count; i++)
            //{
            //    var p = astroPeriods[i];
            //    var s = MarketAnalyzer.AnalyzePeriod(i, p.StartUtc, p.EndUtc, candles);
            //    if (s != null) stats.Add(s);
            //}

            // 3) экспорт summary для Excel
            //MarketAnalyzer.ExportSummaryCsv(@"C:\temp\periods_summary.csv", stats);

            // 4) график
            //var spans = stats.Select(s => (s.StartUtc, s.EndUtc, IsUp: s.ChangePct > 0)).ToList();
            //Plotter.PlotPriceWithPeriods(@"C:\temp\btc_periods.png", candles, spans);
        }

        static List<Candle> LoadCandlesForPeriods(
    string dir,
    string symbol,
    string interval,
    IReadOnlyList<Period> periods)
        {
            var candles = new List<Candle>();

            foreach (var (y, m) in GetMonthsCoveredByPeriods(periods))
            {
                var path = BuildMonthlyPath(dir, symbol, interval, y, m);

                if (!File.Exists(path))
                {
                    Console.WriteLine($"[WARN] Missing file: {Path.GetFileName(path)}");
                    continue;
                }

                candles.AddRange(CandleCsv.Load(path));
            }

            candles.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));
            return candles;
        }
        static string BuildMonthlyPath(string dir, string symbol, string interval, int year, int month)
        {
            return Path.Combine(dir, $"{symbol}_{interval}_{year:D4}-{month:D2}.csv");
        }
        static IEnumerable<(int Year, int Month)> GetMonthsCoveredByPeriods(IReadOnlyList<Period> periods)
        {
            var set = new HashSet<(int Year, int Month)>();

            foreach (var p in periods)
            {
                var cur = new DateTime(p.StartUtc.Year, p.StartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = new DateTime(p.EndUtc.Year, p.EndUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                while (cur <= end)
                {
                    set.Add((cur.Year, cur.Month));
                    cur = cur.AddMonths(1);
                }
            }

            return set.OrderBy(x => x.Year).ThenBy(x => x.Month);
        }

        private static (IReadOnlyList<Period> Periods, SearchRequest Req) GetPeriods(
            SwissEphEphemeris eph,
            IReadOnlyList<SweBody> bodies,
            double diffDeg,
            int step,
            DateTime startUtc,
            DateTime endUtc,
            IEventCondition condition,           // <- добавили
            string exportTag                // <- чтобы файлы не перетирались
        )
        {
            var req = new SearchRequest(
                StartUtc: startUtc,
                EndUtc: endUtc,
                Step: TimeSpan.FromMinutes(step),
                DiffDeg: diffDeg,
                Bodies: bodies
            );

            var engine = new TransitSearchEngine(eph, condition);
            var periods = engine.FindPeriods(req);

            Exporters.ExportPeriodsCsv($@"C:\temp\conjunction_periods_{exportTag}.csv", periods, req, exportTag);
            Exporters.ExportHitsCsv($@"C:\temp\conjunction_hits_{exportTag}.csv", periods, req);

            return (periods, req);
        }

        static IEnumerable<IReadOnlyList<T>> Combinations<T>(IReadOnlyList<T> items, int k)
        {
            int n = items.Count;
            if (k < 0 || k > n) yield break;

            var idx = Enumerable.Range(0, k).ToArray();

            while (true)
            {
                var comb = new T[k];
                for (int i = 0; i < k; i++)
                    comb[i] = items[idx[i]];
                yield return comb;

                int t = k - 1;
                while (t >= 0 && idx[t] == n - k + t) t--;
                if (t < 0) yield break;

                idx[t]++;
                for (int i = t + 1; i < k; i++)
                    idx[i] = idx[i - 1] + 1;
            }
        }

    }
}
