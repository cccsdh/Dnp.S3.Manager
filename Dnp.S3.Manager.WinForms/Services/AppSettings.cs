// -----------------------------------------------------------------------
// <copyright file="AppSettings.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.IO;
using System.Text.Json;

namespace Dnp.S3.Manager.WinForms.Services
{
    public class AppSettings
    {
        private const string FileName = "winforms_settings.json";

        public int MaxConcurrentTransfers { get; set; } = 3;
        public int LargeFolderThreshold { get; set; } = 50;
        public int CompletedRowRetentionSeconds { get; set; } = 5;

        public static AppSettings Load()
        {
            try
            {
                var path = GetPath();
                if (!File.Exists(path)) return new AppSettings();
                var json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                return s ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                var path = GetPath();
                var json = JsonSerializer.Serialize(this);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private static string GetPath()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var app = Path.Combine(dir, "Dnp.S3.Manager");
            Directory.CreateDirectory(app);
            return Path.Combine(app, FileName);
        }
    }
}
