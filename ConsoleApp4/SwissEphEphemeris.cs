using System;
using System.Collections.Generic;
using System.Text;

namespace AstroSwissEph
{
    /// <summary>
    /// Thin wrapper around Swiss Ephemeris: UTC in, longitudes/speeds out.
    /// No scanning, no file IO, no formatting.
    /// </summary>
    public sealed class SwissEphEphemeris : IEphemeris
    {
        private readonly int _flags;

        /// <param name="ephePath">Path to ephemeris data (folder for .se1/.se2 etc).</param>
        /// <param name="flags">SwissEph flags, e.g. SEFLG_SWIEPH|SEFLG_SPEED</param>
        /// <param name="sidereal">If true, enables sidereal mode (also add SEFLG_SIDEREAL to flags).</param>
        /// <param name="sidMode">SwissEph sidereal mode (e.g. 1). Only used if sidereal=true.</param>
        public SwissEphEphemeris(string ephePath, int flags, bool sidereal = false, int sidMode = 1)
        {
            if (string.IsNullOrWhiteSpace(ephePath))
                throw new ArgumentException("ephePath is required", nameof(ephePath));

            SwissEphNative.swe_set_ephe_path(ephePath);
            _flags = flags;

            if (sidereal)
            {
                // Sidereal mode is a configuration, not something to toggle per-step.
                SwissEphNative.swe_set_sid_mode(sidMode, 0, 0);
            }
        }

        public IReadOnlyDictionary<SweBody, BodyState> GetStates(DateTime timeUtc, IReadOnlyList<SweBody> bodies)
        {
            if (timeUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("timeUtc must be DateTimeKind.Utc", nameof(timeUtc));
            if (bodies is null || bodies.Count == 0)
                throw new ArgumentException("bodies must be non-empty", nameof(bodies));

            var jdUt = ToJulianDayUt(timeUtc);

            var dict = new Dictionary<SweBody, BodyState>(bodies.Count);
            foreach (var b in bodies)
                dict[b] = CalcBodyUt(jdUt, (int)b, _flags);

            return dict;
        }

        public static double ToJulianDayUt(DateTime utc)
        {
            var jds = new double[2];
            var err = new StringBuilder(256);

            var sec = utc.Second + utc.Millisecond / 1000.0;

            var rc = SwissEphNative.swe_utc_to_jd(
                utc.Year, utc.Month, utc.Day,
                utc.Hour, utc.Minute, sec,
                SwissEphNative.SE_GREG_CAL,
                jds, err);

            // jds[0] = ET, jds[1] = UT
            return jds[1];
        }

        private static BodyState CalcBodyUt(double jdUt, int planetId, int flags)
        {
            var xx = new double[6];
            var serr = new StringBuilder(256);

            var rc = SwissEphNative.swe_calc_ut(jdUt, planetId, flags, xx, serr);
            // NOTE: rc < 0 indicates an error; serr may contain details.
            // You can choose to throw here if you prefer strictness:
            // if (rc < 0) throw new InvalidOperationException(serr.ToString());

            // xx[0] = longitude, xx[3] = speed in longitude (deg/day)
            return new BodyState(BodyState.Normalize360(xx[0]), xx[3]);
        }
    }
}
