using System;
using System.IO;
using Xunit;
using Dnp.S3.Manager.WinForms.Services;

public class DpapiTests
{
    [Fact]
    public void Secrets_Are_Encrypted_In_Db()
    {
        var db = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
        try
        {
            AccountsSqlStore.EnsureDefaultSettings(db);
            var mgr = new AccountManager { DbPath = db };
            mgr.AddOrUpdate(new Account { Name = "dpapitest", AccessKey = "ak", SecretKey = "secret-value", Region = "r" });

            // read raw SecretKey from DB (should be base64 blob, not the plain secret)
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = db }.ToString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SecretKey FROM Accounts WHERE Name = $name;";
            cmd.Parameters.AddWithValue("$name", "dpapitest");
            var dbVal = cmd.ExecuteScalar() as string;
            Assert.NotNull(dbVal);
            Assert.NotEqual("secret-value", dbVal);

            // ensure loading decrypts to original
            var loaded = AccountsSqlStore.Load(db);
            Assert.Contains(loaded.Accounts, a => a.Name == "dpapitest" && a.SecretKey == "secret-value");
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }
}
