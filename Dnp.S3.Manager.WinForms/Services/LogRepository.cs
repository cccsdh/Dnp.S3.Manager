// -----------------------------------------------------------------------
// <copyright file="LogRepository.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Dnp.S3.Manager.WinForms.Services
{
    public class LogEntry
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
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

        public List<LogEntry> GetLatest(int limit)
        {
            var rows = new List<LogEntry>();
            try
            {
                using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT Id, Timestamp, Level, Message, Exception, Properties FROM Logs ORDER BY Id DESC LIMIT {limit};";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var ts = rdr.IsDBNull(1) ? DateTime.MinValue : DateTime.Parse(rdr.GetString(1));
                    rows.Add(new LogEntry
                    {
                        Id = rdr.IsDBNull(0) ? 0 : rdr.GetInt64(0),
                        Timestamp = ts,
                        Level = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                        Message = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                        Exception = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
                        Properties = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5)
                    });
                }
            }
            catch { }
            return rows;
        }
    }
}
