using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public interface IBtcProvider
    {
        (decimal? open, decimal? high, decimal? low, decimal? close, double? volume) GetOhlcUtc(DateTime utc);
    }
}
