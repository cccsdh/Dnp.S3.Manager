using System;
using System.IO;
using Xunit;
using Dnp.S3.Manager.WinForms.Services;

public class AccountsSqlStoreTests
{
    [Fact]
    public void SqlStoreLoadAndSaveRoundtrip()
    {
        var db = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
        try
        {
            var mgr = new AccountManager();
            mgr.Accounts.Add(new Account { Name = "sqltest", AccessKey = "ak", SecretKey = "sk", Region = "us-east-1" });
            mgr.DbPath = db;
            mgr.Save();

            var loadedAccounts = AccountsSqlStore.Load(db);
            Assert.Contains(loadedAccounts.Accounts, a => a.Name == "sqltest" && a.AccessKey == "ak" && a.SecretKey == "sk");
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }
}
