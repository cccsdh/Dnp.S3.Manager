// -----------------------------------------------------------------------
// <copyright file="DataGridViewProgressColumn.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

namespace Dnp.S3.Manager.WinForms;

public class DataGridViewProgressColumn : DataGridViewTextBoxColumn
{
    public DataGridViewProgressColumn()
    {
        CellTemplate = new DataGridViewProgressCell();
        ValueType = typeof(double);
    }
}

public class DataGridViewProgressCell : DataGridViewTextBoxCell
{
    public DataGridViewProgressCell()
    {
        ValueType = typeof(double);
    }
    // Draw progress directly during Paint using the provided cellBounds.
    // Avoids accessing this.Size (throws for shared/template cells).
    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds,
        int rowIndex, DataGridViewElementStates elementState, object value, object formattedValue,
        string errorText, DataGridViewCellStyle cellStyle,
        DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        try
        {
            // Let base draw background, borders, selection, etc. but skip default content rendering
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, elementState,
                       value, formattedValue, errorText, cellStyle, advancedBorderStyle,
                       paintParts & ~DataGridViewPaintParts.ContentForeground);

            // Only draw progress for actual rows (rowIndex >= 0). If rowIndex < 0, this is a shared/template call.
            if (rowIndex < 0)
            {
                return;
            }

            // Resolve progress value safely
            double progress = 0;
            if (value != null)
            {
                try
                {
                    switch (value)
                    {
                        case double d:
                            progress = d;
                            break;
                        case float f:
                            progress = f;
                            break;
                        case int i:
                            progress = i;
                            break;
                        default:
                            double.TryParse(value.ToString(), out progress);
                            break;
                    }
                }
                catch { progress = 0; }
            }

            // Compute inner rectangle for the progress bar, relative to the cell bounds.
            int innerWidth = Math.Max(1, (int)(cellBounds.Width * 0.9));
            int innerHeight = Math.Max(1, (int)(cellBounds.Height * 0.6));
            int innerX = cellBounds.Left + Math.Max(2, (cellBounds.Width - innerWidth) / 2);
            int innerY = cellBounds.Top + Math.Max(2, (cellBounds.Height - innerHeight) / 2);
            var innerRect = new Rectangle(innerX, innerY, innerWidth, innerHeight);

            // Draw background of progress bar
            using (var bgBrush = new SolidBrush(Color.LightGray))
            {
                graphics.FillRectangle(bgBrush, innerRect);
            }

            // Draw filled portion according to progress (clamped 0..100)
            progress = Math.Max(0.0, Math.Min(100.0, progress));
            int fillWidth = (int)(innerRect.Width * (progress / 100.0));
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(innerRect.Left, innerRect.Top, fillWidth, innerRect.Height);
                using (var fillBrush = new SolidBrush(Color.LightBlue))
                {
                    graphics.FillRectangle(fillBrush, fillRect);
                }
            }
        }
        catch (Exception ex)
        {
            // Swallow painting exceptions to avoid crashing UI. Log for diagnosis.
            Debug.WriteLine($"Progress cell paint error: {ex}");
            try
            {
                base.Paint(graphics, clipBounds, cellBounds, rowIndex, elementState,
                           value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);
            }
            catch { }
        }
    }
}
