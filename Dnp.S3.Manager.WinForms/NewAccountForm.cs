// -----------------------------------------------------------------------
// <copyright file="NewAccountForm.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Windows.Forms;
using Dnp.S3.Manager.WinForms.Services;

namespace Dnp.S3.Manager.WinForms;

public class NewAccountForm : Form
{
    private TextBox nameBox, accessBox, secretBox, regionBox;
    private Button saveBtn, cancelBtn;
    private readonly AccountManager _mgr;
    private readonly Account? _editing;

    public NewAccountForm(AccountManager mgr, Account? editing = null)
    {
        _mgr = mgr ?? new AccountManager();
        _editing = editing;
        Width = 650; Height = 300; Text = editing == null ? "New Account" : "Edit Account";
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        // labels auto-size, inputs take remaining space
        layout.ColumnStyles.Clear();
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.Controls.Add(new Label { Text = "Name" }, 0, 0);
        nameBox = new TextBox { Dock = DockStyle.Fill }; layout.Controls.Add(nameBox, 1, 0);
        layout.Controls.Add(new Label { Text = "Access Key" }, 0, 1);
        accessBox = new TextBox { Dock = DockStyle.Fill }; layout.Controls.Add(accessBox, 1, 1);
        layout.Controls.Add(new Label { Text = "Secret Key" }, 0, 2);
        // NOTE: This is a temporary TextBox for secret input. It will be replaced
        // with the new `Dnp.Controls.Lib.PasswordTextBox` (eye toggle control)
        // in a future refactor so users can show/hide the secret via an icon.
        // Keeping the TextBox for now for simplicity and to avoid introducing
        // the new project reference until the replacement is implemented.
        // mask secret key input
        secretBox = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
        layout.Controls.Add(secretBox, 1, 2);
        layout.Controls.Add(new Label { Text = "Region" }, 0, 3);
        regionBox = new TextBox { Text = "us-east-1", Dock = DockStyle.Fill }; layout.Controls.Add(regionBox, 1, 3);

        saveBtn = new Button { Text = "Save", AutoSize = true, MinimumSize = new System.Drawing.Size(120, 30) };
        cancelBtn = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new System.Drawing.Size(120, 30) };
        saveBtn.Click += (s, e) => { Save(); DialogResult = DialogResult.OK; Close(); };
        cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        // place buttons and ensure they are not clipped
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(saveBtn, 0, 4); layout.Controls.Add(cancelBtn, 1, 4);
        Controls.Add(layout);

        if (_editing != null)
        {
            nameBox.Text = _editing.Name;
            accessBox.Text = _editing.AccessKey;
            secretBox.Text = _editing.SecretKey;
            regionBox.Text = _editing.Region;
        }
    }

    private void Save()
    {
        var acc = new Account { Name = nameBox.Text, AccessKey = accessBox.Text, SecretKey = secretBox.Text, Region = regionBox.Text };
        _mgr.AddOrUpdate(acc);
    }
}
