// -----------------------------------------------------------------------
// <copyright file="AccountsSqlStore.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
// Simple SQLite-backed AccountManager replacement that stores accounts and settings in a single app database.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Dnp.S3.Manager.WinForms.Services
{
    public static class AccountsSqlStore
    {
        public static AccountManager Load(string dbPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? ".");
                EnsureSchema(dbPath);
                var mgr = new AccountManager();
                using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Name, AccessKey, SecretKey, Region FROM Accounts";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var secret = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                    var decrypted = string.Empty;
                    try
                    {
                        if (!string.IsNullOrEmpty(secret))
                        {
                            var blob = Convert.FromBase64String(secret);
                            var dec = System.Security.Cryptography.ProtectedData.Unprotect(blob, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                            decrypted = System.Text.Encoding.UTF8.GetString(dec);
                        }
                    }
                    catch { decrypted = secret; }

                    mgr.Accounts.Add(new Account
                    {
                        Name = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0),
                        AccessKey = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1),
                        SecretKey = decrypted,
                        Region = rdr.IsDBNull(3) ? "us-east-1" : rdr.GetString(3)
                    });
                }
                // attach db path so AccountManager.Save() will persist to DB
                mgr.DbPath = dbPath;
                return mgr;
            }
            catch
            {
                return new AccountManager();
            }
        }

        private static void EnsureSchema(string dbPath)
        {
            using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
            conn.Open();
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Accounts (
  Name TEXT PRIMARY KEY,
  AccessKey TEXT,
  SecretKey TEXT,
  Region TEXT
);
CREATE TABLE IF NOT EXISTS Settings (
  Key TEXT PRIMARY KEY,
  Value TEXT
);
CREATE TABLE IF NOT EXISTS Logs (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Timestamp TEXT,
  Level TEXT,
  Message TEXT,
  Exception TEXT,
  Properties TEXT
);
";
            cmd.ExecuteNonQuery();
            tx.Commit();
        }

        public static void EnsureDefaultSettings(string dbPath)
        {
            EnsureSchema(dbPath);
            using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM Settings;";
            var cnt = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            if (cnt == 0)
            {
                using var tx = conn.BeginTransaction();
                using var ins = conn.CreateCommand();
                ins.CommandText = "INSERT INTO Settings (Key, Value) VALUES ($k, $v);";
                ins.Parameters.AddWithValue("$k", "MaxConcurrentTransfers"); ins.Parameters.AddWithValue("$v", "3"); ins.ExecuteNonQuery();
                ins.Parameters.Clear(); ins.Parameters.AddWithValue("$k", "LargeFolderThreshold"); ins.Parameters.AddWithValue("$v", "50"); ins.ExecuteNonQuery();
                ins.Parameters.Clear(); ins.Parameters.AddWithValue("$k", "CompletedRowRetentionSeconds"); ins.Parameters.AddWithValue("$v", "5"); ins.ExecuteNonQuery();
                tx.Commit();
            }
        }

        public static AppSettings LoadSettings(string dbPath)
        {
            EnsureSchema(dbPath);
            var settings = new AppSettings();
            try
            {
                using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Key, Value FROM Settings;";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var key = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                    var val = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                    if (string.Equals(key, "MaxConcurrentTransfers", StringComparison.OrdinalIgnoreCase)) settings.MaxConcurrentTransfers = int.TryParse(val, out var mct) ? mct : settings.MaxConcurrentTransfers;
                    else if (string.Equals(key, "LargeFolderThreshold", StringComparison.OrdinalIgnoreCase)) settings.LargeFolderThreshold = int.TryParse(val, out var lft) ? lft : settings.LargeFolderThreshold;
                    else if (string.Equals(key, "CompletedRowRetentionSeconds", StringComparison.OrdinalIgnoreCase)) settings.CompletedRowRetentionSeconds = int.TryParse(val, out var crs) ? crs : settings.CompletedRowRetentionSeconds;
                }
            }
            catch { }
            return settings;
        }
    }
}
