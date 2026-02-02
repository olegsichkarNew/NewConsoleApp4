using ConsoleApp4;
using System;

namespace AstroSwissEph
{
    /// <summary>
    /// Example runner (you can copy-paste into your app). Not required by the library itself.
    /// </summary>
    public static class ExampleUsage
    {
        public static void RunExample()
        {
            var eph = new SwissEphEphemeris(
                ephePath: @"C:\my\myPrj\ConsoleApp4\ConsoleApp4\bin\Debug\net9.0\se",
                flags: SwissEphNative.SEFLG_SWIEPH | SwissEphNative.SEFLG_SPEED
                // add | SwissEphNative.SEFLG_SIDEREAL if you enable sidereal=true
            );

            // 1) Classic conjunction by longitude 0..360
            var req = new SearchRequest(
                StartUtc: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndUtc: new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc),
                Step: TimeSpan.FromMinutes(30),
                DiffDeg: 0.5,
                 Bodies: new[] { SweBody.Venus, SweBody.Mercury }
            );

            var engine = new TransitSearchEngine(eph, new ConjunctionLongitudeCondition());
            var periods = engine.FindPeriods(req);

            Exporters.ExportPeriodsCsv(@"C:\temp\conjunction_periods.csv", periods, req, "ConjunctionLongitude");
            Exporters.ExportHitsCsv(@"C:\temp\conjunction_hits.csv", periods, req);

            // 2) Same degree-in-sign (0..30) even if in different signs
            var req2 = req with { DiffDeg = 0.05 };
            var engine2 = new TransitSearchEngine(eph, new SameDegreeInSignCondition());
            var periods2 = engine2.FindPeriods(req2);

            Exporters.ExportPeriodsCsv(@"C:\temp\same_degree_in_sign_periods.csv", periods2, req2, "SameDegreeInSign");
        }
    }
}
