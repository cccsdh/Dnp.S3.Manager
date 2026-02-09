// -----------------------------------------------------------------------
// <copyright file="TransferItem.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;

namespace Dnp.S3.Manager.WinForms
{
    public class TransferItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public double Progress { get; set; }
        public string State { get; set; } = "Queued";
        public string Bucket { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string LocalPath { get; set; } = string.Empty; // local source (upload) or destination (download)
        public bool IsUpload { get; set; } = false;
        public System.Threading.CancellationTokenSource? Cancellation { get; set; }
        // marker used for UI-only placeholder rows to visually fill the grid
        public bool IsPlaceholder { get; set; } = false;
    }
}
