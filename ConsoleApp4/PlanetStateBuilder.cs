using AstroSwissEph;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

public static class PlanetStateBuilder
{
    public static void BuildPlanetState(
        SwissEphEphemeris eph,
        string sqlitePath,
        DateTime startUtc,
        DateTime endUtc,
        TimeSpan step,
        IReadOnlyList<SweBody> bodies,
        int? runId = null)
    {
        if (startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("startUtc/endUtc must be UTC");
        if (endUtc <= startUtc)
            throw new ArgumentException("endUtc must be > startUtc");
        if (step <= TimeSpan.Zero)
            throw new ArgumentException("step must be > 0");
        if (bodies == null || bodies.Count == 0)
            throw new ArgumentException("bodies must be non-empty");

        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = sqlitePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        using var conn = new SqliteConnection(cs);
        conn.Open();

        // Pragmas for faster bulk insert (OK for a generated table)
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=OFF;
PRAGMA temp_store=MEMORY;
PRAGMA cache_size=200000;
";
            pragma.ExecuteNonQuery();
        }

        EnsurePlanetStateTable(conn);

        using var tx = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT OR REPLACE INTO planet_state
(time_utc, body_code, lon, speed, is_retro, is_station, sign, deg_in_sign, run_id)
VALUES
($time_utc, $body_code, $lon, $speed, $is_retro, $is_station, $sign, $deg_in_sign, $run_id);
";

        var pTime = cmd.CreateParameter(); pTime.ParameterName = "$time_utc"; cmd.Parameters.Add(pTime);
        var pBody = cmd.CreateParameter(); pBody.ParameterName = "$body_code"; cmd.Parameters.Add(pBody);
        var pLon = cmd.CreateParameter(); pLon.ParameterName = "$lon"; cmd.Parameters.Add(pLon);
        var pSpeed = cmd.CreateParameter(); pSpeed.ParameterName = "$speed"; cmd.Parameters.Add(pSpeed);
        var pRetro = cmd.CreateParameter(); pRetro.ParameterName = "$is_retro"; cmd.Parameters.Add(pRetro);
        var pStation = cmd.CreateParameter(); pStation.ParameterName = "$is_station"; cmd.Parameters.Add(pStation);
        var pSign = cmd.CreateParameter(); pSign.ParameterName = "$sign"; cmd.Parameters.Add(pSign);
        var pDeg = cmd.CreateParameter(); pDeg.ParameterName = "$deg_in_sign"; cmd.Parameters.Add(pDeg);
        var pRun = cmd.CreateParameter(); pRun.ParameterName = "$run_id"; cmd.Parameters.Add(pRun);

        var t = startUtc;
        var rowCount = 0;

        while (t <= endUtc)
        {
            var states = eph.GetStates(t, bodies);

            foreach (var body in bodies)
            {
                var st = states[body];
                var lon = st.LonDeg;     // already normalized 0..360
                var speed = st.SpeedDegPerDay; // deg/day

                var (sign, degInSign) = ToSign(lon);

                var isRetro = speed < 0 ? 1 : 0;

                // Station: abs(speed) < eps
                var eps = GetStationEpsDegPerDay(body);
                var isStation = Math.Abs(speed) < eps ? 1 : 0;

                pTime.Value = t.ToString("yyyy-MM-ddTHH:mm:ssZ");
                pBody.Value = body.ToString().ToUpperInvariant();
                pLon.Value = lon;
                pSpeed.Value = speed;
                pRetro.Value = isRetro;
                pStation.Value = isStation;
                pSign.Value = sign;
                pDeg.Value = degInSign;
                pRun.Value = (object?)runId ?? DBNull.Value;

                cmd.ExecuteNonQuery();
                rowCount++;
            }

            // коммит батчами, чтобы транзакция не раздувалась (по желанию)
            //if (rowCount % 100_000 == 0)
            //{
            //    tx.Commit();
            //    using var tx2 = conn.BeginTransaction();
            //    cmd.Transaction = tx2;
            //}

            t = t.Add(step);
        }

        // финальный commit
        tx.Commit();
    }

    private static void EnsurePlanetStateTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS planet_state (
  time_utc      TEXT NOT NULL,
  body_code     TEXT NOT NULL,
  lon           REAL NOT NULL,
  speed         REAL NOT NULL,
  is_retro      INTEGER NOT NULL,
  is_station    INTEGER NOT NULL,
  sign          INTEGER NOT NULL,
  deg_in_sign   REAL NOT NULL,
  run_id        INTEGER NULL,
  PRIMARY KEY (time_utc, body_code)
);
CREATE INDEX IF NOT EXISTS ix_planet_state_body_time ON planet_state(body_code, time_utc);
CREATE INDEX IF NOT EXISTS ix_planet_state_time ON planet_state(time_utc);
";
        cmd.ExecuteNonQuery();
    }

    private static (int sign, double degInSign) ToSign(double lon)
    {
        var sign = (int)Math.Floor(lon / 30.0);
        if (sign < 0) sign = 0;
        if (sign > 11) sign = 11;
        var deg = lon - sign * 30.0;
        return (sign, deg);
    }

    private static double GetStationEpsDegPerDay(SweBody body) => body switch
    {
        SweBody.Mercury => 0.01,
        SweBody.Venus => 0.005,
        SweBody.Mars => 0.002,
        SweBody.Jupiter => 0.001,
        SweBody.Saturn => 0.0005,
        SweBody.TrueNode => 0.0005,
        SweBody.MeanNode => 0.0005,
        SweBody.Sun => 0.01,
        SweBody.Moon => 0.05,
        _ => 0.001
    };
}