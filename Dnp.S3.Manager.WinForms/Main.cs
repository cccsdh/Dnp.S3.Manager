// -----------------------------------------------------------------------
// <copyright file="Main.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dnp.S3.Manager.Lib;
using Dnp.S3.Manager.WinForms.Services;
using Microsoft.Extensions.Logging;

namespace Dnp.S3.Manager.WinForms
{
    public partial class Main : Form
    {
        // use the injected account manager; avoid duplicate mutable field to prevent null references
        private Account? currentAccount;
        private S3Client? s3;
        private BindingList<TransferItem> _transfers = new BindingList<TransferItem>();
        private string? _selectedBucket;
        private AppSettings _settings;
        private System.Threading.SemaphoreSlim _transferSemaphore;
        // guard to prevent reentrant calls to EnsureGridFill which can occur when
        // mutating bound lists or the grid's Rows collection while handling grid events
        private bool _inEnsureGridFill = false;
        // suppress handler when programmatically changing selection/content
        private bool _suppressContentSelection = false;

        private readonly Dnp.S3.Manager.WinForms.Services.AccountManager _accounts;
        private readonly Microsoft.Extensions.Logging.ILogger<Main> _logger;
        private readonly Dnp.S3.Manager.WinForms.Services.LogRepository _logRepo;
        private readonly ToolTip _toolTip = new ToolTip();

        public Main(Dnp.S3.Manager.Lib.S3Client s3Client, Dnp.S3.Manager.WinForms.Services.AccountManager accountManager, Microsoft.Extensions.Logging.ILogger<Main> logger, Dnp.S3.Manager.WinForms.Services.LogRepository logRepo)
        {
            s3 = s3Client;
            _accounts = accountManager;
            _logger = logger;
            _logRepo = logRepo;
            InitializeComponent();

            // UI tooltips for primary actions
            _toolTip.SetToolTip(btn_down, "Download the selected object to a local file");
            _toolTip.SetToolTip(btn_up, "Upload a local file to the selected bucket");
            _toolTip.SetToolTip(btn_rename, "Rename the selected object");
            _toolTip.SetToolTip(btn_delete, "Delete the selected object(s)");

            // wire up transfer grid
            dgv_transfer_status.AutoGenerateColumns = false;
            dgv_transfer_status.Columns.Clear();
            dgv_transfer_status.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File", DataPropertyName = "FileName", Width = 400 });
            dgv_transfer_status.Columns.Add(new DataGridViewProgressColumn { HeaderText = "Progress", DataPropertyName = "Progress", Width = 200 });
            dgv_transfer_status.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "State", DataPropertyName = "State", Width = 120 });
            var cancelCol = new DataGridViewButtonColumn { HeaderText = "", UseColumnTextForButtonValue = false, Width = 80 };
            dgv_transfer_status.Columns.Add(cancelCol);
            dgv_transfer_status.CellContentClick += TransfersGrid_CellContentClick;

            // bucket contents grid: image + name + size + modified
            dgv_bucket_contents.AutoGenerateColumns = false;
            dgv_bucket_contents.Columns.Clear();
            dgv_bucket_contents.Columns.Add(new DataGridViewImageColumn { Name = "Icon", Width = 24, ImageLayout = DataGridViewImageCellLayout.Zoom });
            // hidden full key column and isFolder flag
            dgv_bucket_contents.Columns.Add(new DataGridViewTextBoxColumn { Name = "KeyFull", Visible = false });
            dgv_bucket_contents.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsFolder", Visible = false });
            dgv_bucket_contents.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", Name = "NameCol", Width = 400 });
            dgv_bucket_contents.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", Name = "SizeCol", Width = 120 });
            dgv_bucket_contents.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Modified", Name = "DateCol", Width = 200 });
            dgv_bucket_contents.CellDoubleClick += async (s, e) => await OnObjectDoubleClickedAsync(e.RowIndex);
            // single-selection of a folder should drill into it immediately on row enter
            dgv_bucket_contents.RowEnter += async (s, e) =>
            {
                try
                {
                    if (_suppressContentSelection) return;
                    if (e.RowIndex < 0) return;
                    var row = dgv_bucket_contents.Rows[e.RowIndex];
                    if (row == null) return;
                    if (row.Tag != null && row.Tag.ToString() == "__placeholder__") return;
                    var isFolder = (row.Cells["IsFolder"].Value?.ToString() ?? "0") == "1";
                    if (isFolder)
                    {
                        _currentPath = row.Cells["KeyFull"].Value?.ToString() ?? string.Empty;
                        UpdatePathLabel();
                        // suppress re-entrant navigation while changing contents
                        _suppressContentSelection = true;
                        try { await ListObjectsForPrefix(_selectedBucket, _currentPath); }
                        finally { _suppressContentSelection = false; }
                    }
                }
                catch { }
            };
            // ensure bucket contents fills its container even when few rows
            dgv_bucket_contents.BindingContextChanged += (s, e) => EnsureGridFill(dgv_bucket_contents);
            dgv_bucket_contents.RowsAdded += (s, e) => EnsureGridFill(dgv_bucket_contents);
            dgv_bucket_contents.SizeChanged += (s, e) => EnsureGridFill(dgv_bucket_contents);

            // load settings and transfers
            _settings = AppSettings.Load();
            _transferSemaphore = new System.Threading.SemaphoreSlim(_settings.MaxConcurrentTransfers);
            dgv_transfer_status.DataSource = _transfers;

            // wire buttons and menu
            btn_up.Click += async (s, e) => await OnUploadMenuClicked();
            btn_down.Click += async (s, e) => await OnDownloadButtonClicked();
            btn_rename.Click += async (s, e) => await OnRenameClicked();
            btn_delete.Click += async (s, e) => await OnDeleteClicked();

            settingsToolStripMenuItem.Click += (s, e) => 
            {
                var dlg = new SettingsForm(_settings);
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    UpdateSettingsConcurrency(_settings.MaxConcurrentTransfers);
                }
            };

            accountToolStripMenuItem.Click += (s, e) => ShowAccountsDialog();

            // enable drag/drop for bucket contents panel
            dgv_bucket_contents.AllowDrop = true;
            dgv_bucket_contents.DragEnter += RightPanel_DragEnter;
            dgv_bucket_contents.DragDrop += RightPanel_DragDrop;

            // attempt to load bucket icon into treeview ImageList
            try
            {
                var il = new ImageList();
                var bmp = TryGetResourceBitmap("bucket") ?? TryGetResourceBitmap("Bucket");
                if (bmp != null) il.Images.Add("bucket", bmp);
                var fileBmp = TryGetResourceBitmap("file") ?? TryGetResourceBitmap("File");
                if (fileBmp != null) il.Images.Add("file", fileBmp);
                var folderBmp = TryGetResourceBitmap("folder") ?? TryGetResourceBitmap("Folder");
                if (folderBmp != null) il.Images.Add("folder", folderBmp);
                tv_buckets.ImageList = il;
            }
            catch (Exception ex) { _logger.LogError("Error loading resource bitmaps: {Error}", ex.Message); }

            // load accounts and initial state using injected AccountManager
            LoadAccounts();

            // ensure transfer grid fills its container
            dgv_transfer_status.BindingContextChanged += (s, e) => EnsureGridFill(dgv_transfer_status);
            dgv_transfer_status.RowsAdded += (s, e) => EnsureGridFill(dgv_transfer_status);
            dgv_transfer_status.SizeChanged += (s, e) => EnsureGridFill(dgv_transfer_status);

            // s3 client is injected via DI
            if (s3 == null)
            {
                _logger.LogWarning("S3 client not configured at startup. Select an account to initialize the client.");
            }

            // tree selection
            tv_buckets.AfterSelect += async (s, e) =>
            {
                _selectedBucket = e.Node?.Text;
                // reset current path and load root
                _currentPath = string.Empty;
                UpdatePathLabel();
                // suppress selection-driven navigation while loading new bucket contents
                _suppressContentSelection = true;
                try
                {
                    await OnBucketSelectedAsync(_selectedBucket ?? string.Empty);
                }
                finally
                {
                    // ensure no content row is selected after load
                    try { dgv_bucket_contents.ClearSelection(); dgv_bucket_contents.CurrentCell = null; } catch { }
                    _suppressContentSelection = false;
                }
            };

            // prepare logs grid and wire tab selection to refresh logs when Logs tab is shown
            try
            {
                dgv_logs.AutoGenerateColumns = false;
                dgv_logs.Columns.Clear();
                dgv_logs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Timestamp", Name = "colTimestamp", DataPropertyName = "Timestamp", Width = 180 });
                dgv_logs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Level", Name = "colLevel", DataPropertyName = "Level", Width = 80 });
                dgv_logs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", Name = "colMessage", DataPropertyName = "Message", Width = 400 });
                dgv_logs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Exception", Name = "colException", DataPropertyName = "Exception", Width = 400 });
                // refresh when logs tab is selected
                tc_main.SelectedIndexChanged += (s, e) =>
                {
                    if (tc_main.SelectedIndex == 1) RefreshLogs();
                };
            }
            catch { }
        }

        /// <summary>
        /// Ensure the provided DataGridView visually fills its container by adding a few placeholder rows
        /// when the actual data rows are not enough to fill the client area. Placeholder rows are marked
        /// with TransferItem.IsPlaceholder when used in the transfer grid; for other grids generic blank
        /// rows are added as objects with matching properties if possible.
        /// </summary>
        private void EnsureGridFill(DataGridView dgv)
        {
            if (_inEnsureGridFill) return;
            try
            {
                _inEnsureGridFill = true;
                if (dgv == null) return;
                // compute available height for rows (client height minus header)
                var clientHeight = dgv.ClientSize.Height - dgv.ColumnHeadersHeight;
                if (clientHeight <= 0) return;

                var rowHeight = 16;
                if (dgv.RowCount > 0)
                {
                    rowHeight = dgv.Rows[0].Height;
                }

                var needed = clientHeight / Math.Max(1, rowHeight);
                // add a couple extra blank rows for safety
                needed += 2;

                var currentDataRows = dgv.RowCount;
                // if DataSource is a BindingList<TransferItem>, we can add placeholders to that list
                if (dgv.DataSource is BindingList<TransferItem> bl)
                {
                    // remove existing placeholders
                    for (int i = bl.Count - 1; i >= 0; i--)
                    {
                        if (bl[i].IsPlaceholder) bl.RemoveAt(i);
                    }

                    var toAdd = Math.Max(0, needed - bl.Count);
                    for (int i = 0; i < toAdd; i++)
                    {
                        bl.Add(new TransferItem { FileName = string.Empty, Progress = 0, State = string.Empty, IsPlaceholder = true });
                    }
                }
                else
                {
                    // when not bound to TransferItem list, adjust underlying Rows collection
                    // remove existing placeholder rows we added earlier (use Tag)
                    for (int i = dgv.Rows.Count - 1; i >= 0; i--)
                    {
                        var r = dgv.Rows[i];
                        if (r.Tag != null && r.Tag.ToString() == "__placeholder__") dgv.Rows.RemoveAt(i);
                    }

                    var toAdd = Math.Max(0, needed - dgv.Rows.Count);
                    for (int i = 0; i < toAdd; i++)
                    {
                        var idx = dgv.Rows.Add();
                        var r = dgv.Rows[idx];
                        r.Tag = "__placeholder__";
                        // clear cell values using appropriate default types for the column
                        for (int c = 0; c < r.Cells.Count; c++)
                        {
                            var col = dgv.Columns[c];
                            if (col is DataGridViewImageColumn)
                            {
                                // image column: use null (no image) to avoid invalid cast from string
                                r.Cells[c].Value = null;
                            }
                            else
                            {
                                r.Cells[c].Value = string.Empty;
                            }
                        }
                        r.Height = rowHeight;
                    }
                }
            }
            catch { }
            finally
            {
                _inEnsureGridFill = false;
            }
        }

        private void RefreshLogs()
        {
            try
            {
                var rows = _logRepo.GetLatest(500);
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        dgv_logs.DataSource = rows;
                    }));
                }
                else
                {
                    dgv_logs.DataSource = rows;
                }
                AdjustGridRowHeights(dgv_logs);
            }
            catch { }
        }

        private Bitmap? TryGetResourceBitmap(string name)
        {
            try
            {
                var obj = Properties.Resources.ResourceManager.GetObject(name);
                return obj as Bitmap;
            }
            catch { return null; }
        }

        private void LoadAccounts()
        {
            if (_accounts == null) return;
            var names = _accounts.Names.ToList();
            if (names.Count > 0)
            {
                currentAccount = _accounts.Get(names[0]);
                InitializeS3Client();
                _ = RefreshBucketsAsync();
            }
        }

        private void InitializeS3Client()
        {
            if (currentAccount == null) return;
            // if s3 already injected/registered, nothing to do
            if (s3 != null) return;
            try
            {
                // fallback: construct a client for the current account
                s3 = S3Client.CreateWithAws(currentAccount.AccessKey, currentAccount.SecretKey, currentAccount.Region);
            }
            catch { }
        }

        private async Task RefreshBucketsAsync()
        {
            if (s3 == null) return;
            var buckets = await s3.ListBucketsAsync();
            tv_buckets.Nodes.Clear();
            foreach (var b in buckets)
            {
                var node = new TreeNode(b);
                if (tv_buckets.ImageList != null && tv_buckets.ImageList.Images.ContainsKey("bucket")) node.ImageKey = "bucket";
                tv_buckets.Nodes.Add(node);
            }
        }

        private async Task OnBucketSelectedAsync(string bucket)
        {
            if (s3 == null) return;
            if (string.IsNullOrEmpty(bucket)) return;
            await ListObjectsForPrefix(bucket, null);
        }

        private async Task ListObjectsForPrefix(string bucket, string? prefix)
        {
            if (s3 == null) return;
            var res = await s3.ListObjectsAsync(bucket, prefix);
            // suppress selection-driven navigation while we update the grid
            _suppressContentSelection = true;
            dgv_bucket_contents.Rows.Clear();
            var fileBmp = TryGetResourceBitmap("file") ?? TryGetResourceBitmap("File");
            var folderBmp = TryGetResourceBitmap("folder") ?? TryGetResourceBitmap("Folder");

            // folders
            foreach (var p in res.Folders)
            {
                var display = string.IsNullOrEmpty(prefix) ? p.TrimEnd('/') : (p.StartsWith(prefix) ? p.Substring(prefix.Length).TrimEnd('/') : p.TrimEnd('/'));
                Image icon = folderBmp ?? new Bitmap(1, 1);
                var fullKey = p; // prefix returned is full prefix
                dgv_bucket_contents.Rows.Add(icon, fullKey, "1", display, "", "");
            }

            // files
            foreach (var f in res.Files)
            {
                var display = string.IsNullOrEmpty(prefix) ? f.Key : (f.Key.StartsWith(prefix) ? f.Key.Substring(prefix.Length) : f.Key);
                Image icon = fileBmp ?? new Bitmap(1, 1);
                dgv_bucket_contents.Rows.Add(icon, f.Key, "0", display, f.Size.HasValue ? FormatSize(f.Size.Value) : "", f.LastModified?.ToString() ?? "");
            }

            // update current path and label
            _currentPath = prefix ?? string.Empty;
            UpdatePathLabel();
            AdjustGridRowHeights(dgv_bucket_contents);
            // leave rows unselected and clear current cell so RowEnter/Selection events are not fired
            try
            {
                dgv_bucket_contents.ClearSelection();
                dgv_bucket_contents.CurrentCell = null;
            }
            catch { }
            finally
            {
                _suppressContentSelection = false;
            }
        }

        private async Task OnObjectDoubleClickedAsync(int rowIndex)
        {
            if (rowIndex < 0) return;
            if (_selectedBucket == null) return;
            if (rowIndex >= dgv_bucket_contents.Rows.Count) return;
            var row = dgv_bucket_contents.Rows[rowIndex];
            var fullKey = row.Cells[ "KeyFull" ].Value?.ToString() ?? string.Empty;
            var isFolder = (row.Cells["IsFolder"].Value?.ToString() ?? "0") == "1";
            if (isFolder)
            {
                // drill into folder
                _currentPath = fullKey;
                UpdatePathLabel();
                await ListObjectsForPrefix(_selectedBucket, _currentPath);
                return;
            }
            var key = fullKey;
            var save = new SaveFileDialog { FileName = Path.GetFileName(key) };
            if (save.ShowDialog() == DialogResult.OK)
            {
                var file = save.FileName;
                var transfer = new TransferItem { FileName = Path.GetFileName(file), Progress = 0, State = "Queued", Bucket = _selectedBucket, Key = key, LocalPath = file };
                AddTransfer(transfer);
                _ = StartTransferAsync(transfer);
            }
        }

        private void AddTransfer(TransferItem t)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => AddTransfer(t))); return; }
            _transfers.Add(t);
            UpdateTransferButtonAppearance(_transfers.IndexOf(t));
            AdjustGridRowHeights(dgv_transfer_status);
        }

        private bool IsActiveState(string state)
        {
            if (string.IsNullOrEmpty(state)) return false;
            state = state.ToLowerInvariant();
            return state.Contains("starting") || state.Contains("uploading") || state.Contains("downloading") || state.Contains("canceling");
        }

        private void RepositionTransferForState(TransferItem item)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => RepositionTransferForState(item))); return; }
            try
            {
                var idx = _transfers.IndexOf(item);
                if (idx == -1) return;
                if (!string.IsNullOrEmpty(item.State) && item.State.StartsWith("Complete", StringComparison.OrdinalIgnoreCase))
                {
                    var retention = _settings?.CompletedRowRetentionSeconds ?? 5;
                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(retention));
                            if (InvokeRequired) { BeginInvoke(new Action(() =>
                            {
                                var idx2 = _transfers.IndexOf(item);
                                if (idx2 >= 0) _transfers.RemoveAt(idx2);
                            })); }
                            else
                            {
                                var idx2 = _transfers.IndexOf(item);
                                if (idx2 >= 0) _transfers.RemoveAt(idx2);
                            }
                        }
                        catch { }
                    });
                    return;
                }

                if (IsActiveState(item.State))
                {
                    if (idx != 0)
                    {
                        _transfers.RaiseListChangedEvents = false;
                        _transfers.RemoveAt(idx);
                        _transfers.Insert(0, item);
                        _transfers.RaiseListChangedEvents = true;
                        _transfers.ResetBindings();
                    }
                }
                else
                {
                    var lastActive = -1;
                    for (int i = 0; i < _transfers.Count; i++) if (IsActiveState(_transfers[i].State)) lastActive = i;
                    var desired = lastActive + 1;
                    if (idx != desired)
                    {
                        var moving = item;
                        _transfers.RaiseListChangedEvents = false;
                        _transfers.RemoveAt(idx);
                        if (desired >= _transfers.Count) _transfers.Add(moving); else _transfers.Insert(desired, moving);
                        _transfers.RaiseListChangedEvents = true;
                        _transfers.ResetBindings();
                    }
                }
            }
            catch { }
        }

        private void UpdateTransferProgress(string idOrKey, double percent)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => UpdateTransferProgress(idOrKey, percent))); return; }
            var item = _transfers.FirstOrDefault(x => x.Id == idOrKey || x.Key == idOrKey || x.FileName == idOrKey);
            if (item != null)
            {
                item.Progress = percent;
                var idx = _transfers.IndexOf(item);
                _transfers.ResetItem(idx);
                UpdateTransferButtonAppearance(idx);
                AdjustGridRowHeights(dgv_transfer_status);
            }
        }

        private void UpdateTransferState(string idOrKey, string state)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => UpdateTransferState(idOrKey, state))); return; }
            var item = _transfers.FirstOrDefault(x => x.Id == idOrKey || x.Key == idOrKey || x.FileName == idOrKey);
            if (item != null)
            {
                item.State = state;
                var idx = _transfers.IndexOf(item);
                _transfers.ResetItem(idx);
                UpdateTransferButtonAppearance(idx);
                RepositionTransferForState(item);
                AdjustGridRowHeights(dgv_transfer_status);
            }
        }

        private void UpdateTransferButtonAppearance(int idx)
        {
            try
            {
                if (idx < 0 || idx >= dgv_transfer_status.Rows.Count) return;
                var row = dgv_transfer_status.Rows[idx];
                var item = _transfers[idx];
                var btnCell = row.Cells[dgv_transfer_status.Columns.Count - 1] as DataGridViewButtonCell;
                if (btnCell == null) return;

                var state = item.State ?? string.Empty;
                if (state.StartsWith("Complete", StringComparison.OrdinalIgnoreCase))
                {
                    btnCell.Value = "";
                    btnCell.FlatStyle = FlatStyle.Standard;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].Style.ForeColor = Color.Gray;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].Style.BackColor = Color.LightGray;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].ReadOnly = true;
                }
                else if (state.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Canceled", StringComparison.OrdinalIgnoreCase))
                {
                    btnCell.Value = "Retry";
                    row.Cells[dgv_transfer_status.Columns.Count - 1].Style.ForeColor = Color.Black;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].Style.BackColor = SystemColors.Control;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].ReadOnly = false;
                }
                else if (state.Equals("Canceling", StringComparison.OrdinalIgnoreCase))
                {
                    btnCell.Value = "";
                    row.Cells[dgv_transfer_status.Columns.Count - 1].Style.ForeColor = Color.Gray;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].Style.BackColor = Color.LightGray;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].ReadOnly = true;
                }
                else
                {
                    btnCell.Value = "Cancel";
                    row.Cells[dgv_transfer_status.Columns.Count - 1].Style.ForeColor = Color.Black;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].Style.BackColor = SystemColors.Control;
                    row.Cells[dgv_transfer_status.Columns.Count - 1].ReadOnly = false;
                }
            }
            catch { }
        }

        private void TransfersGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex != dgv_transfer_status.Columns.Count - 1) return;
            var item = _transfers[e.RowIndex];
            var cellVal = dgv_transfer_status.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string ?? string.Empty;
            if (string.Equals(cellVal, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (item != null && item.Cancellation != null && !item.Cancellation.IsCancellationRequested)
                {
                    item.Cancellation.Cancel();
                    UpdateTransferState(item.Id, "Canceling");
                }
            }
            else if (string.Equals(cellVal, "Retry", StringComparison.OrdinalIgnoreCase))
            {
                if (item != null)
                {
                    item.Progress = 0;
                    item.State = "Queued";
                    _transfers.ResetItem(e.RowIndex);
                    _ = StartTransferAsync(item);
                }
            }
        }

        private void RightPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else e.Effect = DragDropEffects.None;
        }

        private void RightPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            if (string.IsNullOrEmpty(_selectedBucket))
            {
                MessageBox.Show("Please select a target bucket before dropping files or folders.", "No bucket selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            var largeFolderThreshold = _settings?.LargeFolderThreshold ?? 50; // files
            foreach (var path in files)
            {
                if (Directory.Exists(path))
                {
                    var allFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    if (allFiles.Length > largeFolderThreshold)
                    {
                        var resp = MessageBox.Show($"The folder '{Path.GetFileName(path)}' contains {allFiles.Length} files. Do you want to upload them?", "Large folder", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (resp != DialogResult.Yes)
                        {
                            continue;
                        }
                    }

                    var root = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                    foreach (var f in allFiles)
                    {
                        var rel = Path.GetRelativePath(path, f).Replace(Path.DirectorySeparatorChar, '/');
                        var key = string.IsNullOrEmpty(rel) ? Path.GetFileName(f) : (root + "/" + rel);
                        var transfer = new TransferItem { FileName = Path.GetFileName(f), LocalPath = f, IsUpload = true, Bucket = _selectedBucket, Key = key, Progress = 0, State = "Queued" };
                        AddTransfer(transfer);
                        _ = StartTransferAsync(transfer);
                    }
                }
                else if (File.Exists(path))
                {
                    var key = Path.GetFileName(path);
                    var transfer = new TransferItem { FileName = Path.GetFileName(path), LocalPath = path, IsUpload = true, Bucket = _selectedBucket, Key = key, Progress = 0, State = "Queued" };
                    AddTransfer(transfer);
                    _ = StartTransferAsync(transfer);
                }
            }
        }

        private async Task StartTransferAsync(TransferItem transfer)
        {
            await _transferSemaphore.WaitAsync();
            try
            {
                if (transfer.IsUpload)
                {
                    UpdateTransferState(transfer.Id, "Starting");
                    transfer.Cancellation = new System.Threading.CancellationTokenSource();
                    await using var fs = File.OpenRead(transfer.LocalPath);
                    var progress = new Progress<double>(p => UpdateTransferProgress(transfer.Id, p));
                    UpdateTransferState(transfer.Id, "Uploading");
                    await s3!.PutObjectAsync(transfer.Bucket, transfer.Key, fs, progress: progress, cancellationToken: transfer.Cancellation.Token);
                    UpdateTransferProgress(transfer.Id, 100);
                    UpdateTransferState(transfer.Id, "Complete");
                }
                else
                {
                    UpdateTransferState(transfer.Id, "Starting");
                    transfer.Cancellation = new System.Threading.CancellationTokenSource();
                    var progress = new Progress<double>(p => UpdateTransferProgress(transfer.Id, p));
                    UpdateTransferState(transfer.Id, "Downloading");
                    await s3!.DownloadFileAsync(transfer.Bucket, transfer.Key, transfer.LocalPath, progress, transfer.Cancellation.Token);
                    UpdateTransferProgress(transfer.Id, 100);
                    UpdateTransferState(transfer.Id, "Complete");
                }
            }
            catch (Exception ex)
            {
                UpdateTransferState(transfer.Id, ex is OperationCanceledException ? "Canceled" : "Failed: " + ex.Message);
            }
            finally
            {
                _transferSemaphore.Release();
            }
        }

        private void UpdateSettingsConcurrency(int newLimit)
        {
            try
            {
                _settings.MaxConcurrentTransfers = newLimit;
                _settings.Save();
                var old = _transferSemaphore;
                _transferSemaphore = new System.Threading.SemaphoreSlim(newLimit);
                old.Dispose();
            }
            catch { }
        }

        private async Task OnUploadMenuClicked()
        {
            if (s3 == null)
            {
                MessageBox.Show("S3 client not initialized. Select an account first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(_selectedBucket))
            {
                MessageBox.Show("Select a bucket in the left pane before uploading.", "No bucket selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var ofd = new OpenFileDialog { Multiselect = true };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            foreach (var file in ofd.FileNames)
            {
                var key = Path.GetFileName(file);
                var transfer = new TransferItem { FileName = Path.GetFileName(file), Progress = 0, State = "Queued", Bucket = _selectedBucket, Key = key, LocalPath = file, IsUpload = true };
                AddTransfer(transfer);

                _ = Task.Run(async () =>
                {
                    await _transferSemaphore.WaitAsync();
                    try
                    {
                        UpdateTransferState(transfer.Id, "Starting");
                        transfer.Cancellation = new System.Threading.CancellationTokenSource();
                        await using var fs = File.OpenRead(file);
                        var progress = new Progress<double>(p => UpdateTransferProgress(transfer.Id, p));
                        UpdateTransferState(transfer.Id, "Uploading");
                        await s3!.PutObjectAsync(transfer.Bucket, transfer.Key, fs, progress: progress, cancellationToken: transfer.Cancellation.Token);
                        UpdateTransferProgress(transfer.Id, 100);
                        UpdateTransferState(transfer.Id, "Complete");
                    }
                    catch (Exception ex)
                    {
                        UpdateTransferState(transfer.Id, ex is OperationCanceledException ? "Canceled" : "Failed: " + ex.Message);
                    }
                    finally
                    {
                        _transferSemaphore.Release();
                    }
                });
            }
        }

        private async Task OnDownloadButtonClicked()
        {
            // attempt to download selected item from bucket contents
            if (_selectedBucket == null) return;
            if (dgv_bucket_contents.SelectedRows.Count == 0) return;
            var row = dgv_bucket_contents.SelectedRows[0];
            var key = row.Cells[1].Value?.ToString() ?? string.Empty;
            var save = new SaveFileDialog { FileName = Path.GetFileName(key) };
            if (save.ShowDialog() == DialogResult.OK)
            {
                var dest = save.FileName;
                var transfer = new TransferItem { FileName = Path.GetFileName(dest), Progress = 0, State = "Queued", Bucket = _selectedBucket, Key = key, LocalPath = dest };
                AddTransfer(transfer);
                _ = StartTransferAsync(transfer);
            }
        }

        private async Task OnRenameClicked()
        {
            if (_selectedBucket == null) return;
            if (dgv_bucket_contents.SelectedRows.Count == 0) return;
            var row = dgv_bucket_contents.SelectedRows[0];
            var key = row.Cells[1].Value?.ToString() ?? string.Empty;
            var newName = Microsoft.VisualBasic.Interaction.InputBox("New name:", "Rename", Path.GetFileName(key));
            if (string.IsNullOrEmpty(newName)) return;
            var destKey = Path.Combine(Path.GetDirectoryName(key) ?? string.Empty, newName).Replace('\\', '/');
            await s3!.RenameAsync(_selectedBucket, key, destKey);
            await RefreshBucketsAsync();
        }

        private async Task OnDeleteClicked()
        {
            if (_selectedBucket == null) return;
            if (dgv_bucket_contents.SelectedRows.Count == 0) return;
            var row = dgv_bucket_contents.SelectedRows[0];
            var key = row.Cells[1].Value?.ToString() ?? string.Empty;
            var resp = MessageBox.Show($"Delete {key}?", "Confirm delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (resp != DialogResult.Yes) return;
            await s3!.DeleteObjectsAsync(_selectedBucket, new List<string> { key });
            await RefreshBucketsAsync();
        }

        private void ShowAccountsDialog()
        {
            // pass the injected AccountManager so the dialog mutates the same instance
            var dlg = new AccountsForm(_accounts);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                // do not replace the injected instance; save current state and reload
                _accounts.Save();
                LoadAccounts();
            }
        }

        private static string FormatSize(long? size)
        {
            if (size == null) return string.Empty;
            double s = size.Value;
            if (s < 1024) return s + " B";
            s /= 1024;
            if (s < 1024) return s.ToString("F1") + " KB";
            s /= 1024;
            if (s < 1024) return s.ToString("F1") + " MB";
            s /= 1024;
            return s.ToString("F1") + " GB";
        }

        private void AdjustGridRowHeights(DataGridView dgv)
        {
            try
            {
                if (dgv.RowCount == 0)
                {
                    return;
                }
                // Compute target height so rows fill the client area (integer division may leave small gap)
                var clientHeight = dgv.ClientSize.Height - dgv.ColumnHeadersHeight;
                if (clientHeight <= 0) return;
                var rowCount = Math.Max(1, dgv.RowCount);
                var h = Math.Max(16, clientHeight / rowCount);
                foreach (DataGridViewRow r in dgv.Rows)
                {
                    r.Height = h;
                }
            }
            catch { }
        }

        private string _currentPath = string.Empty;

        private void UpdatePathLabel()
        {
            try
            {
                // build clickable path under ts_path (the small strip under the bucket contents)
                ts_path.Items.Clear();
                ts_path.Items.Add(new ToolStripLabel("PATH:"));

                // helper to create styled breadcrumb button
                ToolStripButton CreateBtn(string text, string tag, EventHandler onClick)
                {
                    var b = new ToolStripButton(text);
                    b.Tag = tag;
                    b.Click += onClick;
                    b.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                    b.ForeColor = Color.FromArgb(30, 30, 30);
                    b.Margin = new Padding(2, 0, 2, 0);
                    return b;
                }

                if (!string.IsNullOrEmpty(_selectedBucket))
                {
                    // root bucket button
                    var rootBtn = CreateBtn(_selectedBucket, string.Empty, async (s, e) => { _currentPath = string.Empty; await ListObjectsForPrefix(_selectedBucket, _currentPath); });
                    ts_path.Items.Add(rootBtn);
                }

                if (!string.IsNullOrEmpty(_currentPath))
                {
                    var trimmed = _currentPath.TrimEnd('/');
                    var segments = trimmed.Split('/');
                    var accum = string.Empty;
                    int maxVisible = 4; // max segments to show before truncating

                    if (segments.Length <= maxVisible)
                    {
                        foreach (var seg in segments)
                        {
                            accum = string.IsNullOrEmpty(accum) ? seg + "/" : accum + seg + "/";
                            ts_path.Items.Add(new ToolStripLabel(" > "));
                            var btn = CreateBtn(seg, accum, async (s, e) => { _currentPath = (s as ToolStripButton)?.Tag as string ?? string.Empty; await ListObjectsForPrefix(_selectedBucket, _currentPath); });
                            ts_path.Items.Add(btn);
                        }
                    }
                    else
                    {
                        // show first, ellipsis dropdown for middle, and last
                        // first segment
                        var first = segments[0];
                        accum = first + "/";
                        ts_path.Items.Add(new ToolStripLabel(" > "));
                        ts_path.Items.Add(CreateBtn(first, accum, async (s, e) => { _currentPath = (s as ToolStripButton)?.Tag as string ?? string.Empty; await ListObjectsForPrefix(_selectedBucket, _currentPath); }));

                        // dropdown for middle segments
                        var dd = new ToolStripDropDownButton("...");
                        dd.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                        dd.Margin = new Padding(2, 0, 2, 0);
                        for (int i = 1; i < segments.Length - 1; i++)
                        {
                            var seg = segments[i];
                            accum += (i == 1 ? string.Empty : "") + seg + "/";
                            var itemSeg = new ToolStripMenuItem(seg);
                            var tag = string.Join('/', segments.Take(i + 1)) + "/";
                            itemSeg.Tag = tag;
                            itemSeg.Click += async (s, e) => { _currentPath = (s as ToolStripMenuItem)?.Tag as string ?? string.Empty; await ListObjectsForPrefix(_selectedBucket, _currentPath); };
                            dd.DropDownItems.Add(itemSeg);
                        }
                        ts_path.Items.Add(new ToolStripLabel(" > "));
                        ts_path.Items.Add(dd);

                        // last segment
                        var last = segments[^1];
                        var lastTag = string.Join('/', segments) + "/";
                        ts_path.Items.Add(new ToolStripLabel(" > "));
                        ts_path.Items.Add(CreateBtn(last, lastTag, async (s, e) => { _currentPath = (s as ToolStripButton)?.Tag as string ?? string.Empty; await ListObjectsForPrefix(_selectedBucket, _currentPath); }));
                    }
                }
            }
            catch { }
        }

        private async void OnPathUpClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath)) return;
            // remove last segment
            var trimmed = _currentPath.TrimEnd('/');
            var idx = trimmed.LastIndexOf('/');
            if (idx >= 0)
            {
                _currentPath = trimmed.Substring(0, idx + 1);
            }
            else
            {
                _currentPath = string.Empty;
            }
            UpdatePathLabel();
            if (!string.IsNullOrEmpty(_selectedBucket)) await ListObjectsForPrefix(_selectedBucket, _currentPath);
        }
    }
}
