using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Dnp.S3.Manager.WinForms.Services
{
    public class LogEntry
    {
        public long Id { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Exception { get; set; } = string.Empty;
        public string Properties { get; set; } = string.Empty;
    }

    public class LogRepository
    {
        private readonly string _dbPath;
        public LogRepository(string dbPath)
        {
            _dbPath = dbPath;
        }

        private string ConnString => new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();

        public List<LogEntry> GetLatest(int limit = 100)
        {
            var list = new List<LogEntry>();
            try
            {
                using var conn = new SqliteConnection(ConnString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, Timestamp, Level, Message, Exception, Properties FROM Logs ORDER BY Id DESC LIMIT $limit;";
                cmd.Parameters.AddWithValue("$limit", limit);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new LogEntry
                    {
                        Id = rdr.GetInt64(0),
                        Timestamp = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                        Level = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                        Message = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                        Exception = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
                        Properties = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5)
                    });
                }
            }
            catch
            {
                // ignore
            }
            return list;
        }
    }
}
