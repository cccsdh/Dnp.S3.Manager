using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Dnp.S3.Manager.WinForms
{
    public partial class Main : Form
    {
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
                    dgv_bucket_contents.RowsAdded += (s, ev) =>
                    {
                        // schedule to run on UI thread after rows added
                        try { BeginInvoke(new Action(() => ApplyIconsToBucketGrid())); } catch { }
                    };

                    // also apply initially in case rows already present
                    try { BeginInvoke(new Action(() => ApplyIconsToBucketGrid())); } catch { }
                }
                catch { }
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
                        // try resource manager first
                        var obj = Properties.Resources.ResourceManager.GetObject(n);
                        if (obj is Bitmap bmp) return bmp;
                    }
                    catch { }
                }

                // fallback to loading PNG files from expected locations (Images folder)
                foreach (var n in candidates)
                {
                    try
                    {
                        var fileName = n + ".png";
                        // try multiple base paths
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

                // ensure icon column is small and centered
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

                // fallback small blank image to avoid nulls
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

                        // set image cell (first column expected to be image)
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
    }
}
