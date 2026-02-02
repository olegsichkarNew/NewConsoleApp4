using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    using System;
    using System.Collections.Generic;

    public sealed class CandlesBtcProvider : IBtcProvider
    {
        private readonly IReadOnlyList<Candle> _candles; // обязательно отсортированы по TimeUtc
        private int _i;

        public CandlesBtcProvider(IReadOnlyList<Candle> candlesSorted)
        {
            _candles = candlesSorted ?? throw new ArgumentNullException(nameof(candlesSorted));
            _i = 0;
        }

        public (decimal? open, decimal? high, decimal? low, decimal? close, double? volume) GetOhlcUtc(DateTime utc)
        {
            // Ищем свечу с временем <= utc (последняя известная)
            while (_i + 1 < _candles.Count && _candles[_i + 1].TimeUtc <= utc)
                _i++;

            if (_candles.Count == 0) return (null, null, null, null, null);

            // Если utc раньше первой свечи — можно вернуть null'ы или первую свечу.
            if (utc < _candles[0].TimeUtc) return (null, null, null, null, null);

            var c = _candles[_i];
            return (c.Open, c.High, c.Low, c.Close, (double) c.Volume);
        }
    }

}
