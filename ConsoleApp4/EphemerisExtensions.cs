using AstroSwissEph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public static class EphemerisExtensions
    {
        public static (double lon, double speed) GetLonSpeedUtc(
            this AstroSwissEph.IEphemeris eph,
            DateTime utc,
            SweBody body)
        {
            var dict = eph.GetStates(utc, new[] { body });
            var s = dict[body];
            return (s.LonDeg, s.SpeedDegPerDay);
        }
    }

}
