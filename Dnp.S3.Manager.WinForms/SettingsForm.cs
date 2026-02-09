// -----------------------------------------------------------------------
// <copyright file="SettingsForm.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Windows.Forms;
using Dnp.S3.Manager.WinForms.Services;

namespace Dnp.S3.Manager.WinForms;

public class SettingsForm : Form
{
    private NumericUpDown concurrencyUpDown;
    private NumericUpDown thresholdUpDown;
    private NumericUpDown retentionUpDown;
    private Button saveBtn;
    private AppSettings _settings;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "Settings";
        Width = 400; Height = 200;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        layout.Controls.Add(new Label { Text = "Max Concurrent Transfers", AutoSize = true }, 0, 0);
        concurrencyUpDown = new NumericUpDown { Minimum = 1, Maximum = 32, Value = _settings.MaxConcurrentTransfers, Dock = DockStyle.Fill };
        layout.Controls.Add(concurrencyUpDown, 1, 0);

        layout.Controls.Add(new Label { Text = "Large Folder Threshold", AutoSize = true }, 0, 1);
        thresholdUpDown = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = _settings.LargeFolderThreshold, Dock = DockStyle.Fill };
        layout.Controls.Add(thresholdUpDown, 1, 1);

        layout.Controls.Add(new Label { Text = "Completed Row Retention (s)", AutoSize = true }, 0, 2);
        retentionUpDown = new NumericUpDown { Minimum = 0, Maximum = 300, Value = _settings.CompletedRowRetentionSeconds, Dock = DockStyle.Fill };
        layout.Controls.Add(retentionUpDown, 1, 2);

        saveBtn = new Button { Text = "Save", Dock = DockStyle.Fill };
        saveBtn.Click += SaveBtn_Click;
        layout.Controls.Add(saveBtn, 1, 3);

        Controls.Add(layout);
    }

    private void SaveBtn_Click(object? sender, EventArgs e)
    {
        _settings.MaxConcurrentTransfers = (int)concurrencyUpDown.Value;
        _settings.LargeFolderThreshold = (int)thresholdUpDown.Value;
        _settings.CompletedRowRetentionSeconds = (int)retentionUpDown.Value;
        _settings.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
