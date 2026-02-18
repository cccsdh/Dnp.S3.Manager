// -----------------------------------------------------------------------
// <copyright file="Main.Extensions.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Dnp.S3.Manager.Lib;

namespace Dnp.S3.Manager.WinForms
{
    public partial class Main : Form
    {
        // cache entry with TTL
        private class CacheEntry
        {
            public ListObjectsResult Result { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
        }

        // lightweight in-memory cache for ListObjects results with TTL
        private readonly ConcurrentDictionary<string, CacheEntry> _listCache = new ConcurrentDictionary<string, CacheEntry>();
        private readonly SemaphoreSlim _prefetchSemaphore = new SemaphoreSlim(3);
        private CancellationTokenSource? _prefetchCts;
        private CancellationTokenSource? _cacheCleanupCts;

        // paging
        private int _pageSize = 200;
        private int _currentPage = 0;
        private string _currentCacheKey = string.Empty;
        private ToolStripButton _tsPrev = null!;
        private ToolStripButton _tsNext = null!;
        private ToolStripLabel _tsPageLabel = null!;

        // virtualization support
        private S3VirtualProvider? _virtualProvider;
        private bool _virtualModeEnabled = true;

        // virtualization provider types
        private class VirtualItem
        {
            public bool IsFolder { get; set; }
            public string Key { get; set; } = string.Empty;
            public long? Size { get; set; }
            public DateTime? Modified { get; set; }
            public string Display { get; set; } = string.Empty;
        }

        private class S3VirtualProvider
        {
            private readonly Main _owner;
            private readonly string _bucket;
            private readonly string _prefix;
            private readonly int _pageSize;
            private readonly List<List<VirtualItem>> _pages = new List<List<VirtualItem>>();
            private readonly List<string?> _continuationTokens = new List<string?>();
            private bool _hasMore = false;
            private bool _loading = false;

            public S3VirtualProvider(Main owner, string bucket, string prefix, int pageSize)
            {
                _owner = owner;
                _bucket = bucket;
                _prefix = prefix ?? string.Empty;
                _pageSize = pageSize;
            }

            public int LoadedCount => _pages.Sum(p => p.Count);
            public bool HasMore => _hasMore;

            public VirtualItem? GetItemAt(int index)
            {
                if (index < 0) return null;
                var pageIndex = index / _pageSize;
                var within = index % _pageSize;
                try { _owner._logger?.LogDebug("S3VirtualProvider.GetItemAt: index={Index}, pageIndex={PageIndex}, within={Within}", index, pageIndex, within); } catch { }
                if (pageIndex < _pages.Count)
                {
                    var page = _pages[pageIndex];
                    if (within < page.Count) return page[within];
                    return null;
                }

                // trigger load of pages up to required page
                try { _owner._logger?.LogDebug("S3VirtualProvider.GetItemAt: triggering EnsurePageLoadedAsync for pageIndex={PageIndex}", pageIndex); } catch { }
                _ = EnsurePageLoadedAsync(pageIndex);
                return null;
            }

            public void SetFromFullResult(ListObjectsResult res)
            {
                _pages.Clear();
                _continuationTokens.Clear();
                _hasMore = false;
                var all = new List<VirtualItem>();
                foreach (var p in res.Folders)
                {
                    all.Add(new VirtualItem { IsFolder = true, Key = p, Display = (p.StartsWith(_prefix) ? p.Substring(_prefix.Length) : p).TrimEnd('/') });
                }
                foreach (var f in res.Files)
                {
                    all.Add(new VirtualItem { IsFolder = false, Key = f.Key, Size = f.Size, Modified = f.LastModified, Display = (f.Key.StartsWith(_prefix) ? f.Key.Substring(_prefix.Length) : f.Key) });
                }
                for (int i = 0; i < all.Count; i += _pageSize)
                {
                    _pages.Add(all.Skip(i).Take(_pageSize).ToList());
                    _continuationTokens.Add(null);
                }
                _hasMore = false;
                try { _owner._logger?.LogInformation("S3VirtualProvider.SetFromFullResult: totalItems={Total}, pages={Pages}", all.Count, _pages.Count); } catch { }
            }

            public async Task InitializeAsync()
            {
                // load first page
                try { _owner._logger?.LogInformation("S3VirtualProvider.InitializeAsync: initializing provider for bucket={Bucket}, prefix={Prefix}", _bucket, _prefix); } catch { }
                await LoadPageAsync(0, null);
            }

            private async Task EnsurePageLoadedAsync(int pageIndex)
            {
                try { _owner._logger?.LogDebug("S3VirtualProvider.EnsurePageLoadedAsync: requested pageIndex={PageIndex}, existingPages={Existing}", pageIndex, _pages.Count); } catch { }
                if (pageIndex < _pages.Count) return;
                // load pages until pageIndex satisfied or no more
                for (int p = _pages.Count; p <= pageIndex; p++)
                {
                    if (!_hasMore && _pages.Count > 0) break;
                    await LoadPageAsync(p, _continuationTokens.Count > 0 ? _continuationTokens.Last() : null);
                }
            }

            private async Task LoadPageAsync(int pageIndex, string? continuationToken)
            {
                if (_loading) return;
                _loading = true;
                try
                {
                    try { _owner._logger?.LogInformation("S3VirtualProvider.LoadPageAsync: loading page={PageIndex}, continuationToken={Token}", pageIndex, continuationToken); } catch { }
                    var raw = await _owner.s3.ListObjectsRawAsync(_bucket, _prefix, _pageSize, continuationToken);
                    var items = new List<VirtualItem>();
                    if (raw.CommonPrefixes != null)
                    {
                        foreach (var cp in raw.CommonPrefixes)
                        {
                            items.Add(new VirtualItem { IsFolder = true, Key = cp, Display = (cp.StartsWith(_prefix) ? cp.Substring(_prefix.Length) : cp).TrimEnd('/') });
                        }
                    }
                    if (raw.S3Objects != null)
                    {
                        foreach (var o in raw.S3Objects.Where(o => !o.Key.EndsWith("/")))
                        {
                            items.Add(new VirtualItem { IsFolder = false, Key = o.Key, Size = o.Size, Modified = o.LastModified, Display = (o.Key.StartsWith(_prefix) ? o.Key.Substring(_prefix.Length) : o.Key) });
                        }
                    }

                    try { _owner._logger?.LogInformation("S3VirtualProvider.LoadPageAsync: page={PageIndex} loaded {Count} items (folders={Folders}, files={Files})", pageIndex, items.Count, raw.CommonPrefixes?.Count ?? 0, raw.S3Objects?.Count ?? 0); } catch { }
                    if (pageIndex < _pages.Count)
                    {
                        // replace if previously placeholder
                        _pages[pageIndex] = items;
                    }
                    else
                    {
                        _pages.Add(items);
                    }

                    _continuationTokens.Add(raw.IsTruncated == true ? raw.NextContinuationToken : null);
                    _hasMore = raw.IsTruncated == true;

                    // notify UI to update row count and refresh
                    try
                    {
                        if (_owner.InvokeRequired) _owner.BeginInvoke(new Action(() =>
                        {
                            _owner.dgv_bucket_contents.RowCount = LoadedCount + (_hasMore ? 1 : 0);
                            _owner.dgv_bucket_contents.Invalidate();
                        }));
                        else
                        {
                            _owner.dgv_bucket_contents.RowCount = LoadedCount + (_hasMore ? 1 : 0);
                            _owner.dgv_bucket_contents.Invalidate();
                        }
                        try { _owner._logger?.LogDebug("S3VirtualProvider.LoadPageAsync: UI updated RowCount={RowCount}", _owner.dgv_bucket_contents.RowCount); } catch { }
                    }
                    catch { }
                }
                catch { }
                finally { _loading = false; }
            }
        }

        // Attach additional wiring on form load: refresh button handler and ensure treeview images
        private void Main_Load(object? sender, EventArgs e)
        {
            try
            {
                // wire refresh button to reload current bucket/path
                try
                {
                    btn_refresh.Click += async (s, ev) => { if (!string.IsNullOrEmpty(_selectedBucket)) await ListObjectsForPrefix(_selectedBucket, _currentPath); };
                }
                catch { }

                // create paging controls (don't add them here because UpdatePathLabel rebuilds ts_path)
                try
                {
                    _tsNext = new ToolStripButton(">") { Alignment = ToolStripItemAlignment.Right };
                    _tsPrev = new ToolStripButton("<") { Alignment = ToolStripItemAlignment.Right };
                    _tsPageLabel = new ToolStripLabel("") { Alignment = ToolStripItemAlignment.Right };
                    _tsPrev.Click += (s, e) => { if (_currentPage > 0) { _currentPage--; TryApplyCurrentCachePage(); } };
                    _tsNext.Click += (s, e) => { _currentPage++; TryApplyCurrentCachePage(); };
                }
                catch { }

                // start cache cleanup task
                try
                {
                    _cacheCleanupCts = new CancellationTokenSource();
                    var token = _cacheCleanupCts.Token;
                    Task.Run(async () =>
                    {
                        while (!token.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromMinutes(1), token);
                                var now = DateTimeOffset.UtcNow;
                                foreach (var kv in _listCache)
                                {
                                    try
                                    {
                                        if (kv.Value.ExpiresAt <= now) _listCache.TryRemove(kv.Key, out _);
                                    }
                                    catch { }
                                }
                            }
                            catch (TaskCanceledException) { break; }
                            catch { }
                        }
                    }, token);
                }
                catch { }

                // ensure treeview ImageList contains bucket/file/folder icons using -32 fallback names
                try
                {
                    var il = tv_buckets.ImageList ?? new ImageList();

                    var bmpBucket = GetResourceBitmapFallback("bucket");
                    if (bmpBucket != null && !il.Images.ContainsKey("bucket")) il.Images.Add("bucket", bmpBucket);

                    var bmpFile = GetResourceBitmapFallback("file");
                    if (bmpFile != null && !il.Images.ContainsKey("file")) il.Images.Add("file", bmpFile);

                    var bmpFolder = GetResourceBitmapFallback("folder");
                    if (bmpFolder != null && !il.Images.ContainsKey("folder")) il.Images.Add("folder", bmpFolder);

                    tv_buckets.ImageList = il;
                }
                catch { }

                // ensure bucket grid icons are applied after rows are added
                try
                {
                    // support virtual mode cell provisioning: attach/detach handled by SetVirtualModeEnabled
                    // try { dgv_bucket_contents.CellValueNeeded += Dgv_bucket_contents_CellValueNeeded; } catch { }

                    dgv_bucket_contents.RowsAdded += (s, ev) =>
                    {
                        // schedule to run on UI thread after rows added
                        try { BeginInvoke(new Action(() => ApplyIconsToBucketGrid())); } catch { }
                    };

                    // also apply initially in case rows already present
                    try { BeginInvoke(new Action(() => ApplyIconsToBucketGrid())); } catch { }

                    // single-click on a content row should also drill into folders
                    dgv_bucket_contents.CellClick += async (s, ev) =>
                    {
                        try
                        {
                            if (ev.RowIndex < 0) return;
                            // if virtual mode, consult provider
                            if (_virtualModeEnabled && _virtualProvider != null)
                            {
                                var vit = _virtualProvider.GetItemAt(ev.RowIndex);
                                if (vit == null) return; // page not yet loaded
                                if (!vit.IsFolder) return;

                                var newPrefix = vit.Key;
                                _currentPath = newPrefix;
                                UpdatePathLabel();
                                _suppressContentSelection = true;
                                try
                                {
                                    var cacheKey = (_selectedBucket ?? string.Empty) + "||" + (newPrefix ?? string.Empty);
                                    var cached = GetFromCache(cacheKey);
                                    if (cached != null)
                                    {
                                        _currentCacheKey = cacheKey;
                                        _currentPage = 0;
                                        ApplyCachedResultToGridPage(cached, newPrefix, _currentPage);
                                    }
                                    else
                                    {
                                        await FetchCacheAndApply(_selectedBucket ?? string.Empty, newPrefix);
                                    }
                                }
                                finally { _suppressContentSelection = false; }

                                return;
                            }

                            var row = dgv_bucket_contents.Rows[ev.RowIndex];
                            if (row == null) return;
                            if (row.Tag != null && row.Tag.ToString() == "__placeholder__") return;

                            string isFolderVal = null;
                            if (dgv_bucket_contents.Columns.Contains("IsFolder"))
                            {
                                isFolderVal = row.Cells["IsFolder"].Value?.ToString();
                            }
                            else if (row.Cells.Count > 2)
                            {
                                isFolderVal = row.Cells[2].Value?.ToString();
                            }

                            var isFolderStr = isFolderVal ?? "0";
                            if (isFolderStr == "1")
                            {
                                var newPrefix = row.Cells["KeyFull"].Value?.ToString() ?? string.Empty;
                                _currentPath = newPrefix;
                                UpdatePathLabel();
                                _suppressContentSelection = true;
                                try
                                {
                                    var cacheKey = (_selectedBucket ?? string.Empty) + "||" + (newPrefix ?? string.Empty);
                                    var cached = GetFromCache(cacheKey);
                                    if (cached != null)
                                    {
                                        // apply cached result immediately (page 0)
                                        _currentCacheKey = cacheKey;
                                        _currentPage = 0;
                                        ApplyCachedResultToGridPage(cached, newPrefix, _currentPage);
                                    }
                                    else
                                    {
                                        // fetch, cache, and apply
                                        await FetchCacheAndApply(_selectedBucket ?? string.Empty, newPrefix);
                                    }
                                }
                                finally { _suppressContentSelection = false; }
                            }
                        }
                        catch { }
                    };
                }
                catch { }

                // attach bucket prefetch handler
                try { AttachBucketPrefetchHandler(); } catch { }

                // ensure CellValueNeeded wiring matches initial virtual mode setting
                try { SetVirtualModeEnabled(_virtualModeEnabled); } catch { }
            }
            catch { }
        }

        private ListObjectsResult? GetFromCache(string cacheKey)
        {
            try
            {
                if (string.IsNullOrEmpty(cacheKey)) return null;
                if (_listCache.TryGetValue(cacheKey, out var entry))
                {
                    if (entry.ExpiresAt > DateTimeOffset.UtcNow) return entry.Result;
                    _listCache.TryRemove(cacheKey, out _);
                }
            }
            catch { }
            return null;
        }

        private void SetCache(string cacheKey, ListObjectsResult res)
        {
            try
            {
                if (string.IsNullOrEmpty(cacheKey) || res == null) return;
                var entry = new CacheEntry { Result = res, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) };
                _listCache[cacheKey] = entry;
            }
            catch { }
        }

        private async Task FetchCacheAndApply(string bucket, string prefix)
        {
            if (s3 == null) return;
            try
            {
                // fast-path: attempt to fetch a single page (max _pageSize) and render it quickly,
                // then fetch the full listing in background and update cache/UI when ready.
                try
                {
                    var raw = await s3.ListObjectsRawAsync(bucket, prefix, _pageSize);
                    var partial = new ListObjectsResult();
                    if (raw.CommonPrefixes != null)
                    {
                        foreach (var p in raw.CommonPrefixes) partial.Folders.Add(p);
                    }
                    if (raw.S3Objects != null)
                    {
                        foreach (var o in raw.S3Objects.Where(o => !o.Key.EndsWith("/")))
                        {
                            partial.Files.Add(new Dnp.S3.Manager.Lib.FileEntry { Key = o.Key, Size = o.Size, LastModified = o.LastModified });
                        }
                    }

                    var cacheKey = bucket + "||" + (prefix ?? string.Empty);
                    // show first page immediately (do not cache partial result as final)
                    _currentCacheKey = cacheKey;
                    _currentPage = 0;
                    ApplyCachedResultToGridPage(partial, prefix, _currentPage);

                    // background: fetch full listing and populate cache, then update UI if still viewing
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var full = await s3.ListObjectsAsync(bucket, prefix);
                            SetCache(cacheKey, full);
                            if (_currentCacheKey == cacheKey)
                            {
                                try
                                {
                                    if (InvokeRequired) BeginInvoke(new Action(() => ApplyCachedResultToGridPage(full, prefix, 0)));
                                    else ApplyCachedResultToGridPage(full, prefix, 0);
                                }
                                catch { }
                            }
                        }
                        catch { }
                    });

                    return;
                }
                catch
                {
                    // ignore and fall through to full fetch
                }

                // fallback: fetch full listing (original behavior)
                var res = await s3.ListObjectsAsync(bucket, prefix);
                var cacheKeyFull = bucket + "||" + (prefix ?? string.Empty);
                SetCache(cacheKeyFull, res);
                _currentCacheKey = cacheKeyFull;
                _currentPage = 0;
                ApplyCachedResultToGridPage(res, prefix, _currentPage);
            }
            catch { }
        }

        private void UpdatePageControls(int totalItems = 0)
        {
            try
            {
                if (_tsPageLabel == null) return;
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)_pageSize));
                if (_currentPage < 0) _currentPage = 0;
                if (_currentPage >= totalPages) _currentPage = totalPages - 1;
                _tsPageLabel.Text = $"Page {_currentPage + 1}/{totalPages}";
                _tsPrev.Enabled = _currentPage > 0;
                _tsNext.Enabled = _currentPage < totalPages - 1;
            }
            catch { }
        }

        private void TryApplyCurrentCachePage()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentCacheKey)) return;
                var cached = GetFromCache(_currentCacheKey);
                if (cached != null)
                {
                    var prefix = _currentCacheKey.Contains("||") ? _currentCacheKey.Split(new[] { "||" }, StringSplitOptions.None)[1] : string.Empty;
                    ApplyCachedResultToGridPage(cached, prefix, _currentPage);
                }
            }
            catch { }
        }

        private void ApplyCachedResultToGridPage(ListObjectsResult res, string? prefix, int page)
        {
            try
            {
                if (res == null) return;

                // Decide whether to use virtual mode for this result
                if (DecideVirtualMode(res))
                {
                    try { _logger?.LogInformation("Applying cached result using virtual provider for prefix={Prefix}", prefix); } catch { }
                    // create and populate virtual provider from full result
                    try
                    {
                        _virtualProvider = new S3VirtualProvider(this, _selectedBucket ?? string.Empty, prefix ?? string.Empty, _pageSize);
                        _virtualProvider.SetFromFullResult(res);
                        SetVirtualModeEnabled(true);
                        // ensure path and paging controls reflect current prefix
                        _currentPage = page;
                        _currentPath = prefix ?? string.Empty;
                        UpdatePathLabel();
                        try { EnsurePagingControlsPresent(); } catch { }
                    }
                    catch (Exception ex) { try { _logger?.LogError(ex, "Failed to initialize virtual provider"); } catch { } }
                    return;
                }

                // ensure we are not in virtual mode when applying concrete paged rows
                try { SetVirtualModeEnabled(false); } catch { }

                dgv_bucket_contents.Rows.Clear();

                var fileBmp = GetResourceBitmapFallback("file") ?? GetResourceBitmapFallback("File");
                var folderBmp = GetResourceBitmapFallback("folder") ?? GetResourceBitmapFallback("Folder");
                var blank = new Bitmap(1, 1);

                var allItems = new System.Collections.Generic.List<(bool isFolder, string key, long? size, DateTime? modified, string display)>();
                foreach (var p in res.Folders)
                {
                    var display = string.IsNullOrEmpty(prefix) ? p.TrimEnd('/') : (p.StartsWith(prefix ?? string.Empty) ? p.Substring((prefix ?? string.Empty).Length).TrimEnd('/') : p.TrimEnd('/'));
                    allItems.Add((true, p, null, null, display));
                }
                foreach (var f in res.Files)
                {
                    var display = string.IsNullOrEmpty(prefix) ? f.Key : (f.Key.StartsWith(prefix ?? string.Empty) ? f.Key.Substring((prefix ?? string.Empty).Length) : f.Key);
                    allItems.Add((false, f.Key, f.Size, f.LastModified, display));
                }

                var totalItems = allItems.Count;
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)_pageSize));
                if (page < 0) page = 0; if (page >= totalPages) page = totalPages - 1;
                var skip = page * _pageSize;
                var pageItems = allItems.Skip(skip).Take(_pageSize);

                foreach (var it in pageItems)
                {
                    if (it.isFolder)
                    {
                        Image icon = folderBmp ?? blank;
                        dgv_bucket_contents.Rows.Add(icon, it.key, "1", it.display, "", null);
                    }
                    else
                    {
                        Image icon = fileBmp ?? blank;
                        dgv_bucket_contents.Rows.Add(icon, it.key, "0", it.display, it.size.HasValue ? FormatSize(it.size.Value) : "", it.modified);
                    }
                }

                _currentPage = page;
                UpdatePageControls(totalItems);

                _currentPath = prefix ?? string.Empty;
                UpdatePathLabel();
                // ensure our paging controls (created in Main_Load) are present after UpdatePathLabel rebuild
                try { EnsurePagingControlsPresent(); } catch { }
                AdjustGridRowHeights(dgv_bucket_contents);
                try { dgv_bucket_contents.ClearSelection(); dgv_bucket_contents.CurrentCell = null; } catch { }
            }
            catch { }
        }

        private Bitmap? GetResourceBitmapFallback(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return null;
                var cap = char.ToUpperInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : string.Empty);
                var candidates = new string[] { name, name + "-32", cap, cap + "-32" };
                foreach (var n in candidates)
                {
                    try
                    {
                        var obj = Properties.Resources.ResourceManager.GetObject(n);
                        if (obj is Bitmap bmp) return bmp;
                    }
                    catch { }
                }

                foreach (var n in candidates)
                {
                    try
                    {
                        var fileName = n + ".png";
                        var bases = new string[] {
                            AppContext.BaseDirectory,
                            Directory.GetCurrentDirectory(),
                            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Dnp.S3.Manager.WinForms"),
                            Path.Combine(AppContext.BaseDirectory, "..", "..", "..")
                        };
                        foreach (var b in bases)
                        {
                            if (string.IsNullOrEmpty(b)) continue;
                            var p1 = Path.Combine(b, "Images", fileName);
                            var p2 = Path.Combine(b, fileName);
                            if (File.Exists(p1)) return new Bitmap(p1);
                            if (File.Exists(p2)) return new Bitmap(p2);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private void ApplyIconsToBucketGrid()
        {
            try
            {
                if (dgv_bucket_contents == null) return;

                if (dgv_bucket_contents.Columns.Count > 0)
                {
                    try
                    {
                        var col = dgv_bucket_contents.Columns[0] as DataGridViewImageColumn;
                        if (col != null)
                        {
                            col.HeaderText = string.Empty;
                            col.Width = 32;
                            col.ImageLayout = DataGridViewImageCellLayout.Zoom;
                            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                            col.Resizable = DataGridViewTriState.False;
                            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                            col.SortMode = DataGridViewColumnSortMode.NotSortable;
                        }
                        else
                        {
                            dgv_bucket_contents.Columns[0].HeaderText = string.Empty;
                            dgv_bucket_contents.Columns[0].Width = 32;
                            dgv_bucket_contents.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                            dgv_bucket_contents.Columns[0].Resizable = DataGridViewTriState.False;
                            dgv_bucket_contents.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                            dgv_bucket_contents.Columns[0].SortMode = DataGridViewColumnSortMode.NotSortable;
                        }
                    }
                    catch { }
                }

                Bitmap? bmpFile = null;
                Bitmap? bmpFolder = null;
                try { bmpFile = GetResourceBitmapFallback("file"); } catch { }
                try { bmpFolder = GetResourceBitmapFallback("folder"); } catch { }

                var blank = new Bitmap(1, 1);

                for (int i = 0; i < dgv_bucket_contents.Rows.Count; i++)
                {
                    try
                    {
                        var row = dgv_bucket_contents.Rows[i];
                        if (row == null) continue;
                        if (row.Tag != null && row.Tag.ToString() == "__placeholder__") continue;

                        string isFolderVal = null;
                        if (dgv_bucket_contents.Columns.Contains("IsFolder"))
                        {
                            isFolderVal = row.Cells["IsFolder"].Value?.ToString();
                        }
                        else if (row.Cells.Count > 2)
                        {
                            isFolderVal = row.Cells[2].Value?.ToString();
                        }

                        var isFolderStr = isFolderVal ?? "0";
                        Image icon = isFolderStr == "1" ? (Image)(bmpFolder ?? blank) : (Image)(bmpFile ?? blank);

                        if (dgv_bucket_contents.Columns.Count > 0)
                        {
                            try { row.Cells[0].Value = icon; } catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Ensure paging controls are present on ts_path after breadcrumb updates
        private void EnsurePagingControlsPresent()
        {
            try
            {
                if (ts_path == null) return;
                if (_tsPageLabel == null || _tsPrev == null || _tsNext == null) return;
                // ensure a spacer exists before right-aligned items
                if (!ts_path.Items.OfType<ToolStripLabel>().Any(l => l.Alignment == ToolStripItemAlignment.Right && string.IsNullOrWhiteSpace(l.Text)))
                {
                    ts_path.Items.Add(new ToolStripLabel(" ") { Alignment = ToolStripItemAlignment.Right });
                }
                if (!ts_path.Items.Contains(_tsPageLabel)) ts_path.Items.Add(_tsPageLabel);
                if (!ts_path.Items.Contains(_tsNext)) ts_path.Items.Add(_tsNext);
                if (!ts_path.Items.Contains(_tsPrev)) ts_path.Items.Add(_tsPrev);
            }
            catch { }
        }

        // attach bucket prefetch handler to start caching root and first-level prefixes
        private void AttachBucketPrefetchHandler()
        {
            try
            {
                tv_buckets.AfterSelect += async (s, e) =>
                {
                    try
                    {
                        var bucket = e.Node?.Text ?? string.Empty;
                        if (string.IsNullOrEmpty(bucket)) return;

                        try { _prefetchCts?.Cancel(); } catch { }
                        _prefetchCts = new CancellationTokenSource();

                        // fetch root and cache it (fire-and-forget)
                        _ = Task.Run(async () => await FetchAndCacheRoot(bucket, _prefetchCts.Token));
                    }
                    catch { }
                };
            }
            catch { }
        }

        private async Task FetchAndCacheRoot(string bucket, CancellationToken token)
        {
            if (s3 == null) return;
            try
            {
                var cacheKey = bucket + "||";
                // fetch root listing
                var res = await s3.ListObjectsAsync(bucket, null);
                SetCache(cacheKey, res);

                // start prefetch for first-level child prefixes
                var toPrefetch = res.Folders.Take(8).ToList();
                await StartPrefetchForPrefixes(bucket, toPrefetch, token);
            }
            catch { }
        }

        private async Task StartPrefetchForPrefixes(string bucket, System.Collections.Generic.List<string> prefixes, CancellationToken token)
        {
            if (s3 == null) return;
            foreach (var p in prefixes)
            {
                if (token.IsCancellationRequested) break;
                var cacheKey = bucket + "||" + (p ?? string.Empty);
                if (_listCache.ContainsKey(cacheKey)) continue;
                try
                {
                    await _prefetchSemaphore.WaitAsync(token);
                    try
                    {
                        var res = await s3.ListObjectsAsync(bucket, p);
                        SetCache(cacheKey, res);
                    }
                    finally { try { _prefetchSemaphore.Release(); } catch { } }
                }
                catch { }
            }
        }

        // cell value provider for virtual mode
        private void Dgv_bucket_contents_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
        {
            try
            {
                if (!_virtualModeEnabled || _virtualProvider == null) return;
                var idx = e.RowIndex;
                var col = e.ColumnIndex;
                var item = _virtualProvider.GetItemAt(idx);
                if (item == null)
                {
                    // show placeholders
                    if (col == 0) e.Value = new Bitmap(1, 1);
                    else e.Value = string.Empty;
                    return;
                }

                var colName = string.Empty;
                try { colName = dgv_bucket_contents.Columns[col].Name; } catch { }

                switch (colName)
                {
                    case "Icon":
                        e.Value = item.IsFolder ? (Image?)GetResourceBitmapFallback("folder") : GetResourceBitmapFallback("file");
                        break;
                    case "KeyFull":
                        e.Value = item.Key;
                        break;
                    case "IsFolder":
                        e.Value = item.IsFolder ? "1" : "0";
                        break;
                    case "NameCol":
                        e.Value = item.Display;
                        break;
                    case "SizeCol":
                        e.Value = item.Size.HasValue ? FormatSize(item.Size.Value) : string.Empty;
                        break;
                    case "DateCol":
                        e.Value = item.Modified.HasValue ? (object)item.Modified.Value : null;
                        break;
                    default:
                        e.Value = string.Empty;
                        break;
                }
            }
            catch { }
        }

        // Enable/disable virtual mode and ensure CellValueNeeded handler is attached/detached
        private void SetVirtualModeEnabled(bool enabled)
        {
            try
            {
                if (dgv_bucket_contents == null) return;

                // avoid duplicate attach
                try { dgv_bucket_contents.CellValueNeeded -= Dgv_bucket_contents_CellValueNeeded; } catch { }

                // log requested change
                try { _logger?.LogInformation("SetVirtualModeEnabled requested: {Enabled}", enabled); } catch { }

                if (enabled)
                {
                    dgv_bucket_contents.CellValueNeeded += Dgv_bucket_contents_CellValueNeeded;
                    dgv_bucket_contents.VirtualMode = true;
                    if (_virtualProvider != null)
                    {
                        dgv_bucket_contents.RowCount = _virtualProvider.LoadedCount + (_virtualProvider.HasMore ? 1 : 0);
                        dgv_bucket_contents.Invalidate();
                        try { _logger?.LogInformation("Virtual mode enabled. RowCount={RowCount}, LoadedCount={LoadedCount}, HasMore={HasMore}", dgv_bucket_contents.RowCount, _virtualProvider.LoadedCount, _virtualProvider.HasMore); } catch { }
                    }
                    else
                    {
                        try { _logger?.LogInformation("Virtual mode enabled but provider is null"); } catch { }
                    }
                }
                else
                {
                    // detach handler and reset virtual settings
                    try { dgv_bucket_contents.CellValueNeeded -= Dgv_bucket_contents_CellValueNeeded; } catch { }
                    dgv_bucket_contents.VirtualMode = false;
                    try { dgv_bucket_contents.RowCount = 0; } catch { }
                    _virtualProvider = null;
                    try { _logger?.LogInformation("Virtual mode disabled"); } catch { }
                }

                _virtualModeEnabled = enabled;
            }
            catch { }
        }

        // Decision helper: determine whether to enable virtual mode for a given listing
        private bool DecideVirtualMode(ListObjectsResult? res)
        {
            try
            {
                if (res == null) return false;
                var total = (res.Folders?.Count ?? 0) + (res.Files?.Count ?? 0);
                // enable virtual mode when total items exceed a few pages
                var enable = total > Math.Max(1, _pageSize * 2);
                try { _logger?.LogInformation("DecideVirtualMode: total={Total}, pageSize={PageSize} => {Enable}", total, _pageSize, enable); } catch { }
                return enable;
            }
            catch { return false; }
        }
    }
}
