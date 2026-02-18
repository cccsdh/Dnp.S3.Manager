// -----------------------------------------------------------------------
// <copyright file="SerilogSqliteSink.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ...existing code...
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SQLite;
using System;

namespace Dnp.S3.Manager.WinForms
{
    public class SerilogSqliteSink
    {
        // This file originally provided a custom Serilog sink; it has been simplified
        // in favor of Serilog.Sinks.SQLite usage in Program.cs. Keep this class for
        // compatibility if there are references elsewhere.

        public static void Configure(string dbPath)
        {
            // no-op: configuration happens in Program.cs
            try { } catch { }
        }
    }
}
