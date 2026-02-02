using System.Runtime.InteropServices;
using System.Text;

namespace AstroSwissEph
{
    /// <summary>Native Swiss Ephemeris bindings. Keep this file minimal.</summary>
    internal static class SwissEphNative
    {
        // Flags you may want (define only what's needed; extend later)
        public const int SEFLG_SWIEPH = 2;
        public const int SEFLG_SPEED = 256;
        public const int SEFLG_SIDEREAL = 64;
        public const int SE_GREG_CAL = 1;

        [DllImport("swedll32.dll", CharSet = CharSet.Ansi)]
        internal static extern void swe_set_ephe_path(string path);

        [DllImport("swedll32.dll")]
        internal static extern int swe_calc_ut(double tjd_ut, int ipl, int iflag, double[] xx, StringBuilder serr);

        [DllImport("swedll32.dll")]
        internal static extern int swe_set_sid_mode(int sid_mode, double t0, double ayan_t0);

        [DllImport("swedll32.dll")]
        internal static extern int swe_utc_to_jd(
            int year, int month, int day,
            int hour, int minute, double second,
            int gregflag,
            double[] julianDayNumbersInEtAndUt,
            StringBuilder errorMessage);
    }
}
