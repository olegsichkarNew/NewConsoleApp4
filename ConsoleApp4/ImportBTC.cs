using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
namespace ConsoleApp4
{
    public sealed record Candle30m(DateTime TimeUtc, double Open, double High, double Low, double Close, double Volume);
    public class ImportBTC
    {
        static void EnsureBtcPriceTable(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS btc_price (
  time_utc TEXT PRIMARY KEY,
  open   REAL NOT NULL,
  high   REAL NOT NULL,
  low    REAL NOT NULL,
  close  REAL NOT NULL,
  volume REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_btc_price_time ON btc_price(time_utc);
";
            cmd.ExecuteNonQuery();
        }

        public static void ImportBtc30mToSqlite(string sqlitePath, IEnumerable<Candle30m> candles30m)
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = sqlitePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            using var conn = new SqliteConnection(cs);
            conn.Open();

            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=OFF;
PRAGMA temp_store=MEMORY;
";
                pragma.ExecuteNonQuery();
            }

            EnsureBtcPriceTable(conn);

            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = @"
INSERT OR REPLACE INTO btc_price(time_utc, open, high, low, close, volume)
VALUES($t, $o, $h, $l, $c, $v);
";

            var pT = cmd.CreateParameter(); pT.ParameterName = "$t"; cmd.Parameters.Add(pT);
            var pO = cmd.CreateParameter(); pO.ParameterName = "$o"; cmd.Parameters.Add(pO);
            var pH = cmd.CreateParameter(); pH.ParameterName = "$h"; cmd.Parameters.Add(pH);
            var pL = cmd.CreateParameter(); pL.ParameterName = "$l"; cmd.Parameters.Add(pL);
            var pC = cmd.CreateParameter(); pC.ParameterName = "$c"; cmd.Parameters.Add(pC);
            var pV = cmd.CreateParameter(); pV.ParameterName = "$v"; cmd.Parameters.Add(pV);

            long n = 0;
            foreach (var x in candles30m)
            {
                pT.Value = x.TimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
                pO.Value = x.Open;
                pH.Value = x.High;
                pL.Value = x.Low;
                pC.Value = x.Close;
                pV.Value = x.Volume;

                cmd.ExecuteNonQuery();
                n++;
            }

            tx.Commit();
            Console.WriteLine($"btc_price rows upserted: {n:n0}");
        }
        public static List<Candle> LoadAllMonthlyCandles(string dir, string symbol, string interval)
        {
            var candles = new List<Candle>();
            foreach (var path in Directory.EnumerateFiles(dir, $"{symbol}_{interval}_*.csv"))
                candles.AddRange(CandleCsv.Load(path));

            candles.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));
            return candles;
        }
        static DateTime FloorTo30m(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc)
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            int minute = utc.Minute;
            int flooredMinute = (minute / 30) * 30;
            return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, flooredMinute, 0, DateTimeKind.Utc);
        }
        public static IEnumerable<Candle30m> ResampleTo30m(List<Candle> candles1m)
        {
            // candles1m должны быть отсортированы по TimeUtc
            Candle currentFirst = null;
            DateTime currentBucket = default;

            double o = 0, h = 0, l = 0, c = 0, v = 0;
            bool hasBucket = false;

            foreach (var x in candles1m)
            {
                var bucket = FloorTo30m(x.TimeUtc);

                if (!hasBucket)
                {
                    // start bucket
                    hasBucket = true;
                    currentBucket = bucket;
                    currentFirst = x;

                    o = (double)x.Open;
                    h = (double)x.High;
                    l = (double)x.Low;
                    c = (double)x.Close;
                    v = (double)x.Volume;
                    continue;
                }

                if (bucket != currentBucket)
                {
                    // flush previous
                    yield return new Candle30m(currentBucket, o, h, l, c, v);

                    // start new
                    currentBucket = bucket;
                    currentFirst = x;

                    o = (double)x.Open;
                    h = (double)x.High;
                    l = (double)x.Low;
                    c = (double)x.Close;
                    v = (double)x.Volume;
                    continue;
                }

                // same bucket
                if ((double)x.High > h) h = (double)x.High;
                if ((double)x.Low < l) l = (double)x.Low;
                c = (double)x.Close;
                v += (double)x.Volume;
            }

            if (hasBucket)
                yield return new Candle30m(currentBucket, o, h, l, c, v);
        }
    }
}
