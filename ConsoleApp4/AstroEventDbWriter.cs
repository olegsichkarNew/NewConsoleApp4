using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    using Microsoft.Data.Sqlite;
    using System;
    using static ConsoleApp4.AstroEvents;


    public sealed class AstroEventDbWriter : IDisposable
    {
        private readonly SqliteConnection _conn;
        private SqliteTransaction? _tx;

        private readonly long _conjunctionTypeId;

        private readonly SqliteCommand _insertEventOrIgnore;
        private readonly SqliteCommand _selectEventId;
        private readonly SqliteCommand _updateEvent;
        private readonly SqliteCommand _upsertEvent;
        private readonly SqliteCommand _upsertEventBody;

        public AstroEventDbWriter(string sqlitePath)
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = sqlitePath,
                ForeignKeys = true
            }.ToString();

            _conn = new SqliteConnection(cs);
            _conn.Open();

            BeginTransaction();

            _conjunctionTypeId = EnsureEventType("CONJUNCTION", "Ecliptic longitude conjunction");

            // 1) event upsert: INSERT OR IGNORE by unique key (event_type_id, period_index, hit_index)
            _insertEventOrIgnore = _conn.CreateCommand();
            _insertEventOrIgnore.Transaction = _tx;
            _insertEventOrIgnore.CommandText = @"
INSERT OR IGNORE INTO fact_event
(event_type_id, period_index, hit_index, minutes_from_start, time_utc, btc_open, btc_high, btc_low, btc_close)
VALUES
($event_type_id, $period_index, $hit_index, $minutes_from_start, $time_utc, $btc_open, $btc_high, $btc_low, $btc_close);
";
            AddParam(_insertEventOrIgnore, "$event_type_id", SqliteType.Integer);
            AddParam(_insertEventOrIgnore, "$period_index", SqliteType.Integer);
            AddParam(_insertEventOrIgnore, "$hit_index", SqliteType.Integer);
            AddParam(_insertEventOrIgnore, "$minutes_from_start", SqliteType.Integer);
            AddParam(_insertEventOrIgnore, "$time_utc", SqliteType.Text);
            AddParam(_insertEventOrIgnore, "$btc_open", SqliteType.Real);
            AddParam(_insertEventOrIgnore, "$btc_high", SqliteType.Real);
            AddParam(_insertEventOrIgnore, "$btc_low", SqliteType.Real);
            AddParam(_insertEventOrIgnore, "$btc_close", SqliteType.Real);

            // 2) resolve event_id
            _selectEventId = _conn.CreateCommand();
            _selectEventId.Transaction = _tx;
            _selectEventId.CommandText = @"
SELECT event_id
FROM fact_event
WHERE event_type_id = $event_type_id
  AND period_index = $period_index
  AND hit_index = $hit_index
LIMIT 1;

";

            AddParam(_selectEventId, "$event_type_id", SqliteType.Integer);
            AddParam(_selectEventId, "$period_index", SqliteType.Integer);
            AddParam(_selectEventId, "$condition_code", SqliteType.Text);
            AddParam(_selectEventId, "$cluster_key", SqliteType.Text);
            AddParam(_selectEventId, "$hit_index", SqliteType.Integer);

            // 3.5) UPSERT fact_event by (event_type_id, period_index, condition_code, cluster_key)
            _upsertEvent = _conn.CreateCommand();
            _upsertEvent.Transaction = _tx;
            _upsertEvent.CommandText = @"
INSERT INTO fact_event
(event_type_id, period_index, hit_index, minutes_from_start, time_utc,
 condition_code, metric, meta_json, cluster_key,
 btc_open, btc_high, btc_low, btc_close, btc_volume,
 btc_ret_prev_hit, btc_ret_from_period_start,
 orb_deg)
VALUES
($event_type_id, $period_index, $hit_index, $minutes_from_start, $time_utc,
 $condition_code, $metric, $meta_json, $cluster_key,
 $btc_open, $btc_high, $btc_low, $btc_close, $btc_volume,
 $btc_ret_prev_hit, $btc_ret_from_period_start,
 $orb_deg)
ON CONFLICT(event_type_id, period_index, hit_index) DO UPDATE SET
  minutes_from_start = excluded.minutes_from_start,
  time_utc = excluded.time_utc,
  condition_code = excluded.condition_code,
  metric = excluded.metric,
  meta_json = excluded.meta_json,
  cluster_key = excluded.cluster_key,
  btc_open = excluded.btc_open,
  btc_high = excluded.btc_high,
  btc_low  = excluded.btc_low,
  btc_close= excluded.btc_close,
  btc_volume = excluded.btc_volume,
  btc_ret_prev_hit = excluded.btc_ret_prev_hit,
  btc_ret_from_period_start = excluded.btc_ret_from_period_start,
  orb_deg = excluded.orb_deg;


";

            AddParam(_upsertEvent, "$event_type_id", SqliteType.Integer);
            AddParam(_upsertEvent, "$period_index", SqliteType.Integer);
            AddParam(_upsertEvent, "$hit_index", SqliteType.Integer);
            AddParam(_upsertEvent, "$minutes_from_start", SqliteType.Integer);
            AddParam(_upsertEvent, "$time_utc", SqliteType.Text);

            AddParam(_upsertEvent, "$condition_code", SqliteType.Text);
            AddParam(_upsertEvent, "$metric", SqliteType.Real);
            AddParam(_upsertEvent, "$meta_json", SqliteType.Text);
            AddParam(_upsertEvent, "$cluster_key", SqliteType.Text);

            AddParam(_upsertEvent, "$btc_open", SqliteType.Real);
            AddParam(_upsertEvent, "$btc_high", SqliteType.Real);
            AddParam(_upsertEvent, "$btc_low", SqliteType.Real);
            AddParam(_upsertEvent, "$btc_close", SqliteType.Real);
            AddParam(_upsertEvent, "$orb_deg", SqliteType.Real);
            AddParam(_upsertEvent, "$btc_volume", SqliteType.Real);
            AddParam(_upsertEvent, "$btc_ret_prev_hit", SqliteType.Real);
            AddParam(_upsertEvent, "$btc_ret_from_period_start", SqliteType.Real);

            // 3) keep event fields fresh (important if you re-run same hit)
            _updateEvent = _conn.CreateCommand();
            _updateEvent.Transaction = _tx;
            _updateEvent.CommandText = @"
UPDATE fact_event
SET minutes_from_start = $minutes_from_start,
    time_utc = $time_utc,
    btc_open = $btc_open,
    btc_high = $btc_high,
    btc_low  = $btc_low,
    btc_close= $btc_close
WHERE event_id = $event_id;
";
            AddParam(_updateEvent, "$minutes_from_start", SqliteType.Integer);
            AddParam(_updateEvent, "$time_utc", SqliteType.Text);
            AddParam(_updateEvent, "$btc_open", SqliteType.Real);
            AddParam(_updateEvent, "$btc_high", SqliteType.Real);
            AddParam(_updateEvent, "$btc_low", SqliteType.Real);
            AddParam(_updateEvent, "$btc_close", SqliteType.Real);
            AddParam(_updateEvent, "$event_id", SqliteType.Integer);

            // 4) upsert participants
            _upsertEventBody = _conn.CreateCommand();
            _upsertEventBody.Transaction = _tx;
            _upsertEventBody.CommandText = @"
INSERT INTO event_body
(event_id, body_id, role, seq, lon, speed, sign, deg_in_sign, is_retro)
VALUES
($event_id, $body_id, $role, $seq, $lon, $speed, $sign, $deg_in_sign, $is_retro)
ON CONFLICT(event_id, body_id) DO UPDATE SET
role = excluded.role,
seq = excluded.seq,
lon = excluded.lon,
speed = excluded.speed,
sign = excluded.sign,
deg_in_sign = excluded.deg_in_sign,
is_retro = excluded.is_retro;
";
            AddParam(_upsertEventBody, "$event_id", SqliteType.Integer);
            AddParam(_upsertEventBody, "$body_id", SqliteType.Integer);
            AddParam(_upsertEventBody, "$role", SqliteType.Text);
            AddParam(_upsertEventBody, "$seq", SqliteType.Integer);
            AddParam(_upsertEventBody, "$lon", SqliteType.Real);
            AddParam(_upsertEventBody, "$speed", SqliteType.Real);
            AddParam(_upsertEventBody, "$sign", SqliteType.Integer);
            AddParam(_upsertEventBody, "$deg_in_sign", SqliteType.Real);
            AddParam(_upsertEventBody, "$is_retro", SqliteType.Integer);
        }
        private void UpsertBody(long eventId, int bodyId, string role, int seq,
    double lon, double speed, int sign, double degInSign)
        {
            _upsertEventBody.Parameters["$event_id"].Value = eventId;
            _upsertEventBody.Parameters["$body_id"].Value = bodyId;
            _upsertEventBody.Parameters["$role"].Value = role;
            _upsertEventBody.Parameters["$seq"].Value = seq;
            _upsertEventBody.Parameters["$lon"].Value = lon;
            _upsertEventBody.Parameters["$speed"].Value = speed;
            _upsertEventBody.Parameters["$sign"].Value = sign;
            _upsertEventBody.Parameters["$deg_in_sign"].Value = degInSign;
            _upsertEventBody.Parameters["$is_retro"].Value = speed < 0 ? 1 : 0;
            _upsertEventBody.ExecuteNonQuery();
        }
        public void SaveEvent(AstroEvent evt)
        {
            long eventTypeId = EnsureEventType(evt.EventTypeCode, evt.EventTypeCode);

            string clusterKey = string.Join(",",
                evt.Bodies.Select(b => (int)b.Body).OrderBy(x => x));

            // UPSERT fact_event
            _upsertEvent.Parameters["$event_type_id"].Value = eventTypeId;
            _upsertEvent.Parameters["$period_index"].Value = evt.PeriodIndex;
            _upsertEvent.Parameters["$hit_index"].Value = evt.HitIndex;
            _upsertEvent.Parameters["$minutes_from_start"].Value = evt.MinutesFromStart;
            _upsertEvent.Parameters["$time_utc"].Value = ToIsoUtc(evt.TimeUtc);

            _upsertEvent.Parameters["$condition_code"].Value = evt.ConditionCode;
            _upsertEvent.Parameters["$metric"].Value = (object?)evt.Metric ?? DBNull.Value;
            _upsertEvent.Parameters["$meta_json"].Value = (object?)evt.MetaJson ?? DBNull.Value;
            _upsertEvent.Parameters["$cluster_key"].Value = clusterKey;

            _upsertEvent.Parameters["$btc_open"].Value = (object?)evt.BtcOpen ?? DBNull.Value;
            _upsertEvent.Parameters["$btc_high"].Value = (object?)evt.BtcHigh ?? DBNull.Value;
            _upsertEvent.Parameters["$btc_low"].Value = (object?)evt.BtcLow ?? DBNull.Value;
            _upsertEvent.Parameters["$btc_close"].Value = (object?)evt.BtcClose ?? DBNull.Value;
            _upsertEvent.Parameters["$orb_deg"].Value = (object?)evt.Metric ?? DBNull.Value;
            _upsertEvent.Parameters["$btc_volume"].Value = (object?)evt.BtcVolume ?? DBNull.Value;
            _upsertEvent.Parameters["$btc_ret_prev_hit"].Value = (object?)evt.BtcRetPrevHit ?? DBNull.Value;
            _upsertEvent.Parameters["$btc_ret_from_period_start"].Value = (object?)evt.BtcRetFromPeriodStart ?? DBNull.Value;

            _upsertEvent.ExecuteNonQuery();

            // resolve event_id
            _selectEventId.Parameters["$event_type_id"].Value = eventTypeId;
            _selectEventId.Parameters["$period_index"].Value = evt.PeriodIndex;
            _selectEventId.Parameters["$condition_code"].Value = evt.ConditionCode;
            _selectEventId.Parameters["$cluster_key"].Value = clusterKey;
            _selectEventId.Parameters["$hit_index"].Value = evt.HitIndex;
            var eventIdObj = _selectEventId.ExecuteScalar()
                ?? throw new InvalidOperationException("Failed to resolve event_id after upsert.");

            long eventId = (long)eventIdObj;

            // UPSERT bodies
            int seq = 0;
            foreach (var b in evt.Bodies)
            {
                _upsertEventBody.Parameters["$event_id"].Value = eventId;
                _upsertEventBody.Parameters["$body_id"].Value = (int)b.Body;
                _upsertEventBody.Parameters["$role"].Value = "participant";
                _upsertEventBody.Parameters["$seq"].Value = seq++;

                _upsertEventBody.Parameters["$lon"].Value = b.Lon;
                _upsertEventBody.Parameters["$speed"].Value = b.Speed;
                _upsertEventBody.Parameters["$sign"].Value = b.Sign;
                _upsertEventBody.Parameters["$deg_in_sign"].Value = b.DegInSign;
                _upsertEventBody.Parameters["$is_retro"].Value = b.Speed < 0 ? 1 : 0;

                _upsertEventBody.ExecuteNonQuery();
            }
        }



        /// <summary>
        /// Commit transaction. Call once after batch.
        /// </summary>
        public void Commit()
        {
            _tx?.Commit();
            _tx?.Dispose();
            _tx = null;

            // Сразу начинаем новую транзакцию, чтобы writer был пригоден дальше
            BeginNewTransaction();
        }
        private void BeginTransaction()
        {
            _tx = _conn.BeginTransaction();

            if (_upsertEvent != null) _upsertEvent.Transaction = _tx;
            if (_selectEventId != null) _selectEventId.Transaction = _tx;
            //if (_insertEventBody != null) _insertEventBody.Transaction = _tx;
        }
        /// <summary>
        /// If you want long-running writer, you can start new transaction after commit.
        /// </summary>
        public void BeginNewTransaction()
        {
            if (_tx != null) throw new InvalidOperationException("Transaction already active.");
            _tx = _conn.BeginTransaction();

            _insertEventOrIgnore.Transaction = _tx;
            _selectEventId.Transaction = _tx;
            _updateEvent.Transaction = _tx;
            _upsertEventBody.Transaction = _tx;
            _upsertEvent.Transaction = _tx;

        }

        public void Dispose()
        {
            _tx?.Dispose();
            _insertEventOrIgnore.Dispose();
            _selectEventId.Dispose();
            _updateEvent.Dispose();
            _upsertEventBody.Dispose();
            _upsertEvent.Dispose();
            _conn.Dispose();
        }

        private long EnsureEventType(string code, string description)
        {
            using var ins = _conn.CreateCommand();
            ins.Transaction = _tx;
            ins.CommandText = @"INSERT OR IGNORE INTO event_type(code, description) VALUES ($c, $d);";
            ins.Parameters.AddWithValue("$c", code);
            ins.Parameters.AddWithValue("$d", description);
            ins.ExecuteNonQuery();

            using var sel = _conn.CreateCommand();
            sel.Transaction = _tx;
            sel.CommandText = @"SELECT event_type_id FROM event_type WHERE code = $c LIMIT 1;";
            sel.Parameters.AddWithValue("$c", code);

            var id = sel.ExecuteScalar();
            if (id is null) throw new InvalidOperationException($"event_type not found: {code}");
            return (long)id;
        }


        

        private static string ToIsoUtc(DateTime dtUtc)
        {
            // important: must be UTC
            var utc = dtUtc.Kind == DateTimeKind.Utc ? dtUtc : dtUtc.ToUniversalTime();
            return utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'");
        }

        private static void AddParam(SqliteCommand cmd, string name, SqliteType type)
            => cmd.Parameters.Add(new SqliteParameter(name, type));
    }

}
