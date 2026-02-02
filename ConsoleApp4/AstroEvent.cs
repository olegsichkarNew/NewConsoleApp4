using AstroSwissEph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public class AstroEvents
    {
        public sealed record EventBodyState(
 SweBody Body,
    double Lon,
    double Speed,
    int Sign,          // 0..11
    double DegInSign   // 0..30
);
        public sealed record AstroEvent(
    string EventTypeCode,      // "CONJUNCTION"
    string ConditionCode,      // "longitude_span" | "same_degree_in_sign"
    int PeriodIndex,
    int HitIndex,
    int MinutesFromStart,
    DateTime TimeUtc,
    IReadOnlyList<EventBodyState> Bodies,
    double? Metric,            // span/ошибка/плотность (что выберем)
    string? MetaJson,          // параметры condition (maxSpanDeg, degree, signMode...)
    double? BtcOpen,
    double? BtcHigh,
    double? BtcLow,
    double? BtcClose,
    double? BtcVolume,
    double? BtcRetPrevHit = null,
    double? BtcRetFromPeriodStart = null
);
        public sealed record ConjunctionSpec(
    string ConditionCode,          // "longitude_span" | "same_degree_in_sign"
    IReadOnlyList<SweBody> BodiesUniverse,
    int MinBodies,                 // 2..N
    double ToleranceDeg,           // например 1.0° или 0.2°
    bool RequireSameSign           // true для ConjunctionLongitudeCondition
);
    }
    public static class AstroMath
    {
        public static double LongitudeSpanDeg(IEnumerable<double> lons)
        {
            var arr = lons
                .Select(Norm360)
                .OrderBy(x => x)
                .ToArray();

            if (arr.Length < 2)
                return 0;

            double maxGap = 0;

            for (int i = 1; i < arr.Length; i++)
                maxGap = Math.Max(maxGap, arr[i] - arr[i - 1]);

            // gap через 360
            maxGap = Math.Max(maxGap, 360 - (arr[^1] - arr[0]));

            return 360 - maxGap; // минимальная дуга
        }

        public static int SignIndex(double lon) // 0..11
            => (int)Math.Floor(Norm360(lon) / 30.0);

        public static double DegInSign(double lon)
            => Norm360(lon) - 30.0 * SignIndex(lon);

        public static double Norm360(double x)
        {
            x %= 360.0;
            if (x < 0) x += 360.0;
            return x;
        }

        // минимальная разница углов [-180..180]
        public static double DeltaAngle(double a, double b)
        {
            var d = Norm360(a) - Norm360(b);
            if (d > 180) d -= 360;
            if (d < -180) d += 360;
            return d;
        }

        public static double AbsDelta(double a, double b) => Math.Abs(DeltaAngle(a, b));
    }

}
