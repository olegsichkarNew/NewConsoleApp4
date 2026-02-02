using System;

namespace AstroSwissEph
{
    public readonly record struct BodyState(double LonDeg, double SpeedDegPerDay)
    {
        public int ZodiacSign => (int)Math.Floor(Normalize360(LonDeg) / 30.0) + 1; // 1..12
        public double DegreeInSign => Normalize360(LonDeg) % 30.0;

        public static double Normalize360(double x)
        {
            x %= 360.0;
            if (x < 0) x += 360.0;
            return x;
        }
    }

    public sealed record Hit(DateTime TimeUtc, System.Collections.Generic.IReadOnlyDictionary<SweBody, BodyState> Bodies);

    public sealed record Period(DateTime StartUtc, DateTime EndUtc, System.Collections.Generic.IReadOnlyList<Hit> Samples);
}
