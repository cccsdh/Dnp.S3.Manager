// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SQLite;
using Dnp.S3.Manager.Lib;

namespace Dnp.S3.Manager.WinForms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // setup simple DI container
        var services = new ServiceCollection();
        // register app services
        // configure application sqlite database path (will contain accounts, settings and logs)
        var appDbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dnp.S3.Manager", "app.db");
        // use maintained Serilog SQLite sink instead of custom sink
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            // Serilog.Sinks.SQLite requires table name parameter - use default 'Logs'
            .WriteTo.SQLite(appDbPath, tableName: "Logs", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug)
            .CreateLogger();

        // register Serilog into Microsoft.Extensions.Logging
        services.AddLogging(loggingBuilder => loggingBuilder.AddProvider(new Serilog.Extensions.Logging.SerilogLoggerProvider(Log.Logger)));
        services.AddSingleton<Dnp.S3.Manager.Lib.IS3ClientFactory, Dnp.S3.Manager.Lib.S3ClientFactory>();
        services.AddSingleton(new Dnp.S3.Manager.WinForms.Services.LogRepository(appDbPath));

        // load settings and accounts early and register them
        // load settings: if db has no settings yet, create defaults in settings table
        // ensure DB schema and default settings on first run
        Dnp.S3.Manager.WinForms.Services.AccountsSqlStore.EnsureDefaultSettings(appDbPath);
        var appSettings = Dnp.S3.Manager.WinForms.Services.AccountsSqlStore.LoadSettings(appDbPath);
        services.AddSingleton(appSettings);
        var accounts = Dnp.S3.Manager.WinForms.Services.AccountsSqlStore.Load(appDbPath);
        services.AddSingleton(accounts);

        // register provider and factory
        services.AddSingleton<Dnp.S3.Manager.Lib.IS3ClientProvider, Dnp.S3.Manager.Lib.S3ClientProvider>();
        services.AddSingleton<Dnp.S3.Manager.Lib.IS3ClientFactory, Dnp.S3.Manager.Lib.S3ClientFactory>();

        // register S3Client singleton created from first configured account (if available)
        services.AddSingleton<Dnp.S3.Manager.Lib.S3Client>(sp =>
        {
            var accMgr = sp.GetRequiredService<Dnp.S3.Manager.WinForms.Services.AccountManager>();
            var factory = sp.GetRequiredService<Dnp.S3.Manager.Lib.IS3ClientFactory>();
            var provider = sp.GetRequiredService<Dnp.S3.Manager.Lib.IS3ClientProvider>();
            var names = accMgr.Names.ToList();
            if (names.Count == 0) return null!; // will throw if requested and not configured
            var acc = accMgr.Get(names[0]);
            var client = factory.Create(acc.AccessKey, acc.SecretKey, acc.Region);
            provider.SetClient(client);
            return client;
        });

        // register Main for DI
        services.AddTransient<Main>();

        // build provider
        var provider = services.BuildServiceProvider();

        // resolve Main from DI so its dependencies are injected
        var mainForm = provider.GetRequiredService<Main>();
        Application.Run(mainForm);
    }
}
