using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using WPMOverlay.Models;

namespace WPMOverlay.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private static readonly string DbFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WPMOverlay");
        private static readonly string DbFilePath = Path.Combine(DbFolder, "typing_history.db");

        public DatabaseService()
        {
            if (!Directory.Exists(DbFolder))
            {
                Directory.CreateDirectory(DbFolder);
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DbFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using (var pragmaCmd = new SqliteCommand("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;", connection))
            {
                pragmaCmd.ExecuteNonQuery();
            }

            string createTablesSql = @"
                CREATE TABLE IF NOT EXISTS sessions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    started_at TEXT NOT NULL,
                    ended_at TEXT NOT NULL,
                    duration_seconds REAL NOT NULL,
                    gross_keystrokes INTEGER NOT NULL,
                    backspaces INTEGER NOT NULL,
                    net_words REAL NOT NULL,
                    avg_wpm REAL NOT NULL,
                    peak_15s_wpm REAL NOT NULL,
                    peak_30s_wpm REAL NOT NULL,
                    peak_60s_wpm REAL NOT NULL,
                    accuracy_percent REAL NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_sessions_started ON sessions(started_at);
            ";

            using var command = new SqliteCommand(createTablesSql, connection);
            command.ExecuteNonQuery();
        }

        public void SaveSession(TypingSession session)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                string insertSql = @"
                    INSERT INTO sessions (
                        started_at, ended_at, duration_seconds, gross_keystrokes,
                        backspaces, net_words, avg_wpm, peak_15s_wpm,
                        peak_30s_wpm, peak_60s_wpm, accuracy_percent
                    ) VALUES (
                        @started_at, @ended_at, @duration_seconds, @gross_keystrokes,
                        @backspaces, @net_words, @avg_wpm, @peak_15s_wpm,
                        @peak_30s_wpm, @peak_60s_wpm, @accuracy_percent
                    );
                    SELECT last_insert_rowid();
                ";

                using var command = new SqliteCommand(insertSql, connection);
                command.Parameters.AddWithValue("@started_at", session.StartedAt.ToString("o"));
                command.Parameters.AddWithValue("@ended_at", session.EndedAt.ToString("o"));
                command.Parameters.AddWithValue("@duration_seconds", session.DurationSeconds);
                command.Parameters.AddWithValue("@gross_keystrokes", session.GrossKeystrokes);
                command.Parameters.AddWithValue("@backspaces", session.Backspaces);
                command.Parameters.AddWithValue("@net_words", session.NetWords);
                command.Parameters.AddWithValue("@avg_wpm", session.AvgWpm);
                command.Parameters.AddWithValue("@peak_15s_wpm", session.Peak15sWpm);
                command.Parameters.AddWithValue("@peak_30s_wpm", session.Peak30sWpm);
                command.Parameters.AddWithValue("@peak_60s_wpm", session.Peak60sWpm);
                command.Parameters.AddWithValue("@accuracy_percent", session.AccuracyPercent);

                var idObj = command.ExecuteScalar();
                if (idObj != null && long.TryParse(idObj.ToString(), out long newId))
                {
                    session.Id = newId;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session: {ex.Message}");
            }
        }

        public StatsSummary GetStatsSummary()
        {
            var summary = new StatsSummary();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // 1. All-time peak & averages & total words
                string sqlSummary = @"
                    SELECT 
                        COALESCE(MAX(CASE WHEN peak_15s_wpm > peak_30s_wpm AND peak_15s_wpm > avg_wpm THEN peak_15s_wpm ELSE avg_wpm END), 0) AS top_wpm,
                        COALESCE(AVG(avg_wpm), 0) AS avg_wpm,
                        COALESCE(SUM(net_words), 0) AS total_words,
                        COUNT(*) AS session_count
                    FROM sessions;
                ";

                using (var cmd = new SqliteCommand(sqlSummary, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        summary.AllTimeTopWpm = reader.GetDouble(0);
                        summary.OverallAvgWpm = reader.GetDouble(1);
                        summary.TotalWordsTyped = reader.GetDouble(2);
                        summary.TotalSessions = reader.GetInt32(3);
                    }
                }

                // 2. Today's Top
                string todayStartIso = DateTime.UtcNow.Date.ToString("o");
                string sqlToday = @"
                    SELECT COALESCE(MAX(CASE WHEN peak_15s_wpm > avg_wpm THEN peak_15s_wpm ELSE avg_wpm END), 0)
                    FROM sessions
                    WHERE started_at >= @today_start;
                ";

                using (var cmdToday = new SqliteCommand(sqlToday, connection))
                {
                    cmdToday.Parameters.AddWithValue("@today_start", todayStartIso);
                    var val = cmdToday.ExecuteScalar();
                    if (val != null && double.TryParse(val.ToString(), out double topToday))
                    {
                        summary.TodayTopWpm = topToday;
                    }
                }

                // 3. Last Session
                string sqlLast = @"
                    SELECT avg_wpm, peak_15s_wpm, peak_30s_wpm, peak_60s_wpm
                    FROM sessions
                    ORDER BY id DESC
                    LIMIT 1;
                ";

                using (var cmdLast = new SqliteCommand(sqlLast, connection))
                using (var reader = cmdLast.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        summary.LastSessionWpm = reader.GetDouble(0);
                        summary.LastSession15sWpm = reader.GetDouble(1);
                        summary.LastSession30sWpm = reader.GetDouble(2);
                        summary.LastSession60sWpm = reader.GetDouble(3);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error querying stats summary: {ex.Message}");
            }

            return summary;
        }

        public List<TypingSession> GetRecentSessions(int limit = 100)
        {
            var sessions = new List<TypingSession>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                string query = @"
                    SELECT id, started_at, ended_at, duration_seconds,
                           gross_keystrokes, backspaces, net_words, avg_wpm,
                           peak_15s_wpm, peak_30s_wpm, peak_60s_wpm, accuracy_percent
                    FROM sessions
                    ORDER BY id DESC
                    LIMIT @limit;
                ";

                using var cmd = new SqliteCommand(query, connection);
                cmd.Parameters.AddWithValue("@limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var s = new TypingSession
                    {
                        Id = reader.GetInt64(0),
                        StartedAt = DateTime.Parse(reader.GetString(1)),
                        EndedAt = DateTime.Parse(reader.GetString(2)),
                        DurationSeconds = reader.GetDouble(3),
                        GrossKeystrokes = reader.GetInt32(4),
                        Backspaces = reader.GetInt32(5),
                        NetWords = reader.GetDouble(6),
                        AvgWpm = reader.GetDouble(7),
                        Peak15sWpm = reader.GetDouble(8),
                        Peak30sWpm = reader.GetDouble(9),
                        Peak60sWpm = reader.GetDouble(10),
                        AccuracyPercent = reader.GetDouble(11)
                    };
                    sessions.Add(s);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving sessions: {ex.Message}");
            }

            return sessions;
        }

        public bool DeleteSession(long id)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var cmd = new SqliteCommand("DELETE FROM sessions WHERE id = @id;", connection);
                cmd.Parameters.AddWithValue("@id", id);
                int rows = cmd.ExecuteNonQuery();
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting session {id}: {ex.Message}");
                return false;
            }
        }

        public int DeleteSessions(IEnumerable<long> ids)
        {
            int deletedCount = 0;
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var transaction = connection.BeginTransaction();

                using var cmd = new SqliteCommand("DELETE FROM sessions WHERE id = @id;", connection, transaction);
                var idParam = cmd.Parameters.Add("@id", SqliteType.Integer);

                foreach (var id in ids)
                {
                    idParam.Value = id;
                    deletedCount += cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error batch deleting sessions: {ex.Message}");
            }

            return deletedCount;
        }

        public void ClearHistory()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var cmd = new SqliteCommand("DELETE FROM sessions;", connection);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing history: {ex.Message}");
            }
        }

        public void ExportToCsv(string filePath)
        {
            var sessions = GetRecentSessions(10000);
            var sb = new StringBuilder();
            sb.AppendLine("Id,Date,DurationSeconds,GrossKeys,Backspaces,AccuracyPercent,NetWords,AvgWpm,Peak15sWpm,Peak30sWpm,Peak60sWpm");

            foreach (var s in sessions)
            {
                sb.AppendLine($"{s.Id},{s.StartedAt:yyyy-MM-dd HH:mm:ss},{s.DurationSeconds:F2},{s.GrossKeystrokes},{s.Backspaces},{s.AccuracyPercent:F1},{s.NetWords:F1},{s.AvgWpm:F1},{s.Peak15sWpm:F1},{s.Peak30sWpm:F1},{s.Peak60sWpm:F1}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
