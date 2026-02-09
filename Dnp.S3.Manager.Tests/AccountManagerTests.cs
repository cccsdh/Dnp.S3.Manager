using System;
using System.IO;
using Xunit;
using Dnp.S3.Manager.WinForms.Services;

public class AccountManagerTests
{
    [Fact]
    public void Save_Throws_WhenNoDbPath()
    {
        var mgr = new AccountManager();
        Assert.Throws<InvalidOperationException>(() => mgr.Save());
    }

    [Fact]
    public void AddOrUpdate_Persists_And_Updates()
    {
        var db = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
        try
        {
            // ensure schema exists
            AccountsSqlStore.EnsureDefaultSettings(db);

            var mgr = new AccountManager { DbPath = db };
            mgr.AddOrUpdate(new Account { Name = "dup", AccessKey = "ak1", SecretKey = "sk1", Region = "r1" });
            // update same name
            mgr.AddOrUpdate(new Account { Name = "dup", AccessKey = "ak2", SecretKey = "sk2", Region = "r2" });

            var loaded = AccountsSqlStore.Load(db);
            Assert.Contains(loaded.Accounts, a => a.Name == "dup" && a.AccessKey == "ak2" && a.SecretKey == "sk2");
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }

    [Fact]
    public void Remove_Persists_Removal()
    {
        var db = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
        try
        {
            AccountsSqlStore.EnsureDefaultSettings(db);
            var mgr = new AccountManager { DbPath = db };
            mgr.AddOrUpdate(new Account { Name = "toremove", AccessKey = "a", SecretKey = "s", Region = "r" });
            var before = AccountsSqlStore.Load(db);
            Assert.Contains(before.Accounts, a => a.Name == "toremove");

            var removed = mgr.Remove("toremove");
            Assert.True(removed);

            var after = AccountsSqlStore.Load(db);
            Assert.DoesNotContain(after.Accounts, a => a.Name == "toremove");
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }

    [Fact]
    public void EmptyFields_Persist_AsEmptyStrings()
    {
        var db = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
        try
        {
            AccountsSqlStore.EnsureDefaultSettings(db);
            var mgr = new AccountManager { DbPath = db };
            mgr.AddOrUpdate(new Account { Name = "empty", AccessKey = "", SecretKey = "", Region = "" });
            var loaded = AccountsSqlStore.Load(db);
            Assert.Contains(loaded.Accounts, a => a.Name == "empty" && a.AccessKey == string.Empty && a.SecretKey == string.Empty);
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }
}
