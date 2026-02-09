// -----------------------------------------------------------------------
// <copyright file="AccountManager.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Reflection;

namespace Dnp.S3.Manager.WinForms.Services
{
    public class Account
    {
        public string Name { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Region { get; set; } = "us-east-1";
    }

    public class AccountManager
    {
        // File-based behavior removed. This manager now requires a configured DbPath to persist accounts.
        public List<Account> Accounts { get; set; } = new List<Account>();
        public int LastIndex { get; set; } = -1;
        // When non-null, Save/Remove operations will persist to this sqlite database path
        public string? DbPath { get; set; }

        public IEnumerable<string> Names => Accounts.Select(a => a.Name);
        public IEnumerable<Account> GetAll() => Accounts;

        public void Save()
        {
            if (string.IsNullOrEmpty(DbPath))
            {
                throw new InvalidOperationException("AccountManager is not configured with a DbPath. File-based persistence has been removed.");
            }
            // persist each account into the DB (upsert)
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = DbPath }.ToString());
                conn.Open();
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                foreach (var a in Accounts)
                {
                    // encrypt secret with DPAPI
                    var secretBytes = System.Text.Encoding.UTF8.GetBytes(a.SecretKey ?? string.Empty);
                    var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(secretBytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                    var secretB64 = Convert.ToBase64String(protectedBytes);

                    cmd.CommandText = "INSERT INTO Accounts (Name, AccessKey, SecretKey, Region) VALUES ($name, $access, $secret, $region) ON CONFLICT(Name) DO UPDATE SET AccessKey=$access, SecretKey=$secret, Region=$region;";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("$name", a.Name);
                    cmd.Parameters.AddWithValue("$access", a.AccessKey ?? string.Empty);
                    cmd.Parameters.AddWithValue("$secret", secretB64);
                    cmd.Parameters.AddWithValue("$region", a.Region ?? "us-east-1");
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                try { Serilog.Log.Error(ex, "Failed to save accounts to DB {DbPath}", DbPath); } catch { }
                throw;
            }
        }

        public void AddOrUpdate(Account acc)
        {
            var existing = Accounts.FirstOrDefault(a => a.Name == acc.Name);
            if (existing != null)
            {
                existing.AccessKey = acc.AccessKey;
                existing.SecretKey = acc.SecretKey;
                existing.Region = acc.Region;
            }
            else
            {
                Accounts.Add(acc);
            }
            LastIndex = Accounts.FindIndex(a => a.Name == acc.Name);
            // persist change immediately if DB-backed
            if (!string.IsNullOrEmpty(DbPath)) Save();
        }

        public bool Remove(string name)
        {
            var existing = Accounts.FirstOrDefault(a => a.Name == name);
            if (existing == null) return false;
            Accounts.Remove(existing);
            if (LastIndex >= Accounts.Count) LastIndex = Accounts.Count - 1;
            if (string.IsNullOrEmpty(DbPath)) return true;

            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = DbPath }.ToString());
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Accounts WHERE Name = $name";
                cmd.Parameters.AddWithValue("$name", name);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                try { Serilog.Log.Error(ex, "Failed to remove account {Name} from DB {DbPath}", name, DbPath); } catch { }
                throw;
            }
        }

        public Account? Get(string name) => Accounts.FirstOrDefault(a => a.Name == name);
        public int GetLastIndex() => LastIndex;
        public void SetLastIndex(int idx) => LastIndex = idx;
    }
}
