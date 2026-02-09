// -----------------------------------------------------------------------
// <copyright file="AccountsForm.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Windows.Forms;
using Dnp.S3.Manager.WinForms.Services;

namespace Dnp.S3.Manager.WinForms;

public class AccountsForm : Form
{
    private ListBox listBox;
    private Button addBtn, editBtn, deleteBtn, okBtn, cancelBtn;
    private readonly AccountManager _accounts;

    public AccountsForm(AccountManager mgr)
    {
        _accounts = mgr ?? new AccountManager();
        Width = 600; Height = 400; Text = "Accounts";
        listBox = new ListBox { Dock = DockStyle.Top, Height = 300 };
        Controls.Add(listBox);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
        addBtn = new Button { Text = "Add" }; editBtn = new Button { Text = "Edit" }; deleteBtn = new Button { Text = "Delete" }; okBtn = new Button { Text = "OK" }; cancelBtn = new Button { Text = "Cancel" };
        panel.Controls.Add(addBtn); panel.Controls.Add(editBtn); panel.Controls.Add(deleteBtn); panel.Controls.Add(okBtn); panel.Controls.Add(cancelBtn);
        Controls.Add(panel);

        addBtn.Click += (s, e) => { var nf = new NewAccountForm(_accounts); if (nf.ShowDialog() == DialogResult.OK) RefreshList(); };
        editBtn.Click += (s, e) =>
        {
            if (listBox.SelectedItem == null) return;
            var name = listBox.SelectedItem.ToString() ?? string.Empty;
            var acc = _accounts.Get(name);
            if (acc == null) return;
            var nf = new NewAccountForm(_accounts, acc);
            if (nf.ShowDialog() == DialogResult.OK) RefreshList();
        };
        deleteBtn.Click += (s, e) =>
        {
            if (listBox.SelectedItem == null) return;
            var name = listBox.SelectedItem.ToString() ?? string.Empty;
            var resp = MessageBox.Show($"Delete account '{name}'?", "Confirm delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (resp != DialogResult.Yes) return;
            _accounts.Remove(name);
            RefreshList();
        };
        okBtn.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
        cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        RefreshList();
    }

    private void RefreshList()
    {
        listBox.Items.Clear();
        foreach (var a in _accounts.GetAll()) listBox.Items.Add(a.Name);
    }
}
