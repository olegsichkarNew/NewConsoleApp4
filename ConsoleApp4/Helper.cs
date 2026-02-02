using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public static class Helper
    {
        public static string F4(decimal x) =>
x.ToString("0.0000", CultureInfo.InvariantCulture);
        public static string F4(double x) =>
x.ToString("0.0000", CultureInfo.InvariantCulture);
        public static string F3(decimal x) =>
            x.ToString("0.000", CultureInfo.InvariantCulture);

        public static string F0(decimal x) =>
            x.ToString("0", CultureInfo.InvariantCulture);
        public static string F2(decimal x) => x.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
