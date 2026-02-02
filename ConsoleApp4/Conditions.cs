using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroSwissEph
{
    /// <summary>Classic conjunction: compare in ecliptic longitude space 0..360 (wrap-safe). Supports 2+ bodies.</summary>
    public sealed class ConjunctionLongitudeCondition : IEventCondition
    {
        public bool IsMatch(IReadOnlyDictionary<SweBody, BodyState> states, SearchRequest req)
        {
            var angles = req.Bodies.Select(b => states[b].LonDeg).ToArray();
            return CircularRange(angles, 360.0) <= req.DiffDeg;
        }

        internal static double CircularRange(double[] angles, double modulus)
        {
            var arr = angles.Select(a => Normalize(a, modulus)).ToArray();
            Array.Sort(arr);

            double maxGap = 0;
            for (int i = 1; i < arr.Length; i++)
                maxGap = Math.Max(maxGap, arr[i] - arr[i - 1]);

            // wrap gap
            maxGap = Math.Max(maxGap, (arr[0] + modulus) - arr[^1]);

            // min covering arc
            return modulus - maxGap;
        }

        private static double Normalize(double x, double m)
        {
            x %= m;
            if (x < 0) x += m;
            return x;
        }
    }

    /// <summary>
    /// Same "degree in sign": compare lon%30 on a 0..30 circle. Signs may differ. Supports 2+ bodies.
    /// </summary>
    public sealed class SameDegreeInSignCondition : IEventCondition
    {
        public bool IsMatch(IReadOnlyDictionary<SweBody, BodyState> states, SearchRequest req)
        {
            var degs = req.Bodies.Select(b => states[b].DegreeInSign).ToArray();
            return ConjunctionLongitudeCondition.CircularRange(degs, 30.0) <= req.DiffDeg;
        }
    }

    /// <summary>Retrograde period for a single body (speed&lt;0). For convenience, still uses SearchRequest.Bodies[0].</summary>
    public sealed class RetrogradeCondition : IEventCondition
    {
        public bool IsMatch(IReadOnlyDictionary<SweBody, BodyState> states, SearchRequest req)
        {
            if (req.Bodies.Count != 1) throw new ArgumentException("RetrogradeCondition requires exactly 1 body.");
            var b = req.Bodies[0];
            return states[b].SpeedDegPerDay < 0;
        }
    }
}
