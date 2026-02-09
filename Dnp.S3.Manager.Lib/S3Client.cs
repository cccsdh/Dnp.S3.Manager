// -----------------------------------------------------------------------
// <copyright file="S3Client.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;

namespace Dnp.S3.Manager.Lib
{
    public enum LogVerbosity
    {
        Terse,
        Verbose
    }

    public class FileEntry
    {
        public string Key { get; set; }
        public long? Size { get; set; }
        public DateTime? LastModified { get; set; }
    }

    public class ListObjectsResult
    {
        public List<string> Folders { get; set; } = new List<string>();
        public List<FileEntry> Files { get; set; } = new List<FileEntry>();
    }

    public class S3Client
    {
        private readonly IS3Api _client;
        private readonly ITransferUtility _transferUtility;
        private readonly ILogger? _logger;
        private readonly LogVerbosity _verbosity;

        // primary constructor used in production
        public S3Client(string accessKey, string secretKey, string region, LogVerbosity verbosity = LogVerbosity.Terse, ILogger? logger = null)
        {
            var creds = new BasicAWSCredentials(accessKey, secretKey);
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);
            var realClient = new AmazonS3Client(creds, regionEndpoint);
            _client = new AmazonS3ApiAdapter(realClient);
            _transferUtility = new TransferUtilityAdapter(realClient);
            _logger = logger;
            _verbosity = verbosity;
        }

        // testable constructor allowing dependency injection
        public S3Client(IS3Api client, ITransferUtility transferUtility, LogVerbosity verbosity = LogVerbosity.Terse, ILogger? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _transferUtility = transferUtility ?? throw new ArgumentNullException(nameof(transferUtility));
            _logger = logger;
            _verbosity = verbosity;
        }

        // abstraction for IAmazonS3 to make the API mockable in tests
        public interface IS3Api
        {
            Task<ListBucketsResponse> ListBucketsAsync(System.Threading.CancellationToken cancellationToken = default);
            Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, System.Threading.CancellationToken cancellationToken = default);
            Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, System.Threading.CancellationToken cancellationToken = default);
            Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request, System.Threading.CancellationToken cancellationToken = default);
            Task<DeleteObjectsResponse> DeleteObjectsAsync(DeleteObjectsRequest request, System.Threading.CancellationToken cancellationToken = default);
        }

        private class AmazonS3ApiAdapter : IS3Api
        {
            private readonly IAmazonS3 _impl;
            public AmazonS3ApiAdapter(IAmazonS3 impl) { _impl = impl; }
            public Task<ListBucketsResponse> ListBucketsAsync(System.Threading.CancellationToken cancellationToken = default) => _impl.ListBucketsAsync(cancellationToken);
            public Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, System.Threading.CancellationToken cancellationToken = default) => _impl.ListObjectsV2Async(request, cancellationToken);
            public Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, System.Threading.CancellationToken cancellationToken = default) => _impl.GetObjectAsync(request, cancellationToken);
            public Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request, System.Threading.CancellationToken cancellationToken = default) => _impl.CopyObjectAsync(request, cancellationToken);
            public Task<DeleteObjectsResponse> DeleteObjectsAsync(DeleteObjectsRequest request, System.Threading.CancellationToken cancellationToken = default) => _impl.DeleteObjectsAsync(request, cancellationToken);
        }

        // adapter interface and default implementation for TransferUtility to enable testing
        public interface ITransferUtility
        {
            Task UploadAsync(TransferUtilityUploadRequest request, System.Threading.CancellationToken cancellationToken = default);
        }

        private class TransferUtilityAdapter : ITransferUtility
        {
            private readonly TransferUtility _impl;
            public TransferUtilityAdapter(IAmazonS3 client) { _impl = new TransferUtility(client); }
            public Task UploadAsync(TransferUtilityUploadRequest request, System.Threading.CancellationToken cancellationToken = default) => _impl.UploadAsync(request, cancellationToken);
        }

        // Factory to create a production S3Client backed by real AWS SDK types
        public static S3Client CreateWithAws(string accessKey, string secretKey, string region, LogVerbosity verbosity = LogVerbosity.Terse, ILogger? logger = null)
        {
            var creds = new BasicAWSCredentials(accessKey, secretKey);
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);
            var realClient = new AmazonS3Client(creds, regionEndpoint);
            return new S3Client(new AmazonS3ApiAdapter(realClient), new TransferUtilityAdapter(realClient), verbosity, logger);
        }

        private void Log(string message, bool verboseOnly = false)
        {
            if (_logger == null) return;
            if (verboseOnly && _verbosity == LogVerbosity.Terse) return;
            try { _logger.LogInformation(message); } catch { }
        }

        public async Task<List<string>> ListBucketsAsync()
        {
            Log("Listing buckets", verboseOnly: false);
            try
            {
                var res = await _client.ListBucketsAsync();
                var list = res.Buckets.Select(b => b.BucketName).ToList();
                Log($"Found {list.Count} buckets", verboseOnly: true);
                return list;
            }
            catch (Exception ex)
            {
                _logger?.LogError("ListBucketsAsync failed: {Error}", ex.Message);
                throw;
            }
        }

        public async Task<ListObjectsV2Response> ListObjectsRawAsync(string bucket, string prefix = null, int maxKeys = 1000)
        {
            Log($"Listing objects raw for bucket={bucket}, prefix={prefix}", verboseOnly: true);
            var req = new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix ?? string.Empty,
                Delimiter = "/",
                MaxKeys = maxKeys
            };
            try
            {
                return await _client.ListObjectsV2Async(req);
            }
            catch (Exception ex)
            {
                _logger?.LogError("ListObjectsRawAsync failed for {Bucket}/{Prefix}: {Error}", bucket, prefix, ex.Message);
                throw;
            }
        }

        public async Task<ListObjectsResult> ListObjectsAsync(string bucket, string prefix = null, int maxKeys = 1000)
        {
            Log($"Listing objects for bucket={bucket}, prefix={prefix}", verboseOnly: false);

            try
            {
                var result = new ListObjectsResult();
                var seenFolders = new HashSet<string>();
                var seenFiles = new HashSet<string>();
                string? continuationToken = null;
                do
                {
                    var req = new ListObjectsV2Request
                    {
                        BucketName = bucket,
                        Prefix = prefix ?? string.Empty,
                        Delimiter = "/",
                        MaxKeys = maxKeys,
                        ContinuationToken = continuationToken
                    };

                    var res = await _client.ListObjectsV2Async(req);

                    if (res.CommonPrefixes != null)
                    {
                        foreach (var p in res.CommonPrefixes)
                        {
                            if (seenFolders.Add(p)) result.Folders.Add(p);
                        }
                    }

                    if (res.S3Objects != null)
                    {
                        foreach (var o in res.S3Objects.Where(o => !o.Key.EndsWith("/")))
                        {
                            if (!seenFiles.Contains(o.Key))
                            {
                                seenFiles.Add(o.Key);
                                result.Files.Add(new FileEntry
                                {
                                    Key = o.Key,
                                    Size = o.Size,
                                    LastModified = o.LastModified
                                });
                            }
                        }
                    }

                    continuationToken = res.IsTruncated == true ? res.NextContinuationToken : null;
                } while (continuationToken != null);

                Log($"Found {result.Folders.Count} folders and {result.Files.Count} files", verboseOnly: true);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError("ListObjectsAsync failed for {Bucket}/{Prefix}: {Error}", bucket, prefix, ex.Message);
                throw;
            }
        }

        public async Task PutObjectAsync(string bucket, string key, Stream inputStream, string contentType = "application/octet-stream", IProgress<double>? progress = null)
        {
            Log($"Putting object {key} to bucket {bucket}", verboseOnly: false);

            // Use injected transfer utility to provide progress events
            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = inputStream,
                ContentType = contentType,
                AutoCloseStream = false
            };

            if (progress != null)
            {
                uploadRequest.UploadProgressEvent += (sender, args) =>
                {
                    try
                    {
                        if (args.TotalBytes > 0)
                        {
                            double pct = (double)args.TransferredBytes / args.TotalBytes * 100.0;
                            progress.Report(pct);
                        }
                    }
                    catch { }
                };
            }

            try
            {
                await _transferUtility.UploadAsync(uploadRequest);
                if (progress != null) progress.Report(100.0);
                Log($"Put object {key}", verboseOnly: true);
            }
            catch (Exception ex)
            {
                _logger?.LogError("PutObjectAsync failed for {Bucket}/{Key}: {Error}", bucket, key, ex.Message);
                throw;
            }
        }

        public async Task PutObjectAsync(string bucket, string key, Stream inputStream, string contentType = "application/octet-stream", IProgress<double>? progress = null, System.Threading.CancellationToken cancellationToken = default)
        {
            Log($"Putting object {key} to bucket {bucket}", verboseOnly: false);

            // Use injected transfer utility to provide progress events
            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = inputStream,
                ContentType = contentType,
                AutoCloseStream = false
            };

            if (progress != null)
            {
                uploadRequest.UploadProgressEvent += (sender, args) =>
                {
                    try
                    {
                        if (args.TotalBytes > 0)
                        {
                            double pct = (double)args.TransferredBytes / args.TotalBytes * 100.0;
                            progress.Report(pct);
                        }
                    }
                    catch { }
                };
            }

            try
            {
                await _transferUtility.UploadAsync(uploadRequest, cancellationToken);
                if (progress != null) progress.Report(100.0);
                Log($"Put object {key}", verboseOnly: true);
            }
            catch (Exception ex)
            {
                _logger?.LogError("PutObjectAsync (cancellation) failed for {Bucket}/{Key}: {Error}", bucket, key, ex.Message);
                throw;
            }
        }

        public async Task<GetObjectResponse> GetObjectAsync(string bucket, string key)
        {
            Log($"Getting object {key} from bucket {bucket}", verboseOnly: true);
            try
            {
                var req = new GetObjectRequest { BucketName = bucket, Key = key };
                return await _client.GetObjectAsync(req);
            }
            catch (Exception ex)
            {
                _logger?.LogError("GetObjectAsync failed for {Bucket}/{Key}: {Error}", bucket, key, ex.Message);
                throw;
            }
        }

        public async Task<GetObjectResponse> GetObjectAsync(string bucket, string key, System.Threading.CancellationToken cancellationToken = default)
        {
            Log($"Getting object {key} from bucket {bucket}", verboseOnly: true);
            try
            {
                var req = new GetObjectRequest { BucketName = bucket, Key = key };
                return await _client.GetObjectAsync(req, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError("GetObjectAsync (cancellation) failed for {Bucket}/{Key}: {Error}", bucket, key, ex.Message);
                throw;
            }
        }

        public async Task DownloadFileAsync(string bucket, string key, string destinationPath, IProgress<double>? progress = null)
        {
            Log($"Downloading s3://{bucket}/{key} to {destinationPath}", verboseOnly: false);
            var res = await GetObjectAsync(bucket, key);
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var total = res.ContentLength;
            var buffer = new byte[81920];
            long readSoFar = 0;
            await using (var outStream = File.Create(destinationPath))
            await using (var inStream = res.ResponseStream)
            {
                int read;
                while ((read = await inStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await outStream.WriteAsync(buffer, 0, read);
                    readSoFar += read;
                    if (total > 0 && progress != null)
                    {
                        double pct = (double)readSoFar / total * 100.0;
                        progress.Report(pct);
                    }
                }
            }

            if (progress != null) progress.Report(100.0);
            Log($"Downloaded to {destinationPath}", verboseOnly: true);
        }

        public async Task DownloadFileAsync(string bucket, string key, string destinationPath, IProgress<double>? progress = null, System.Threading.CancellationToken cancellationToken = default)
        {
            Log($"Downloading s3://{bucket}/{key} to {destinationPath}", verboseOnly: false);
            var res = await GetObjectAsync(bucket, key, cancellationToken);
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var total = res.ContentLength;
            var buffer = new byte[81920];
            long readSoFar = 0;
            await using (var outStream = File.Create(destinationPath))
            await using (var inStream = res.ResponseStream)
            {
                int read;
                while ((read = await inStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await outStream.WriteAsync(buffer, 0, read, cancellationToken);
                    readSoFar += read;
                    if (total > 0 && progress != null)
                    {
                        double pct = (double)readSoFar / total * 100.0;
                        progress.Report(pct);
                    }
                }
            }

            if (progress != null) progress.Report(100.0);
            Log($"Downloaded to {destinationPath}", verboseOnly: true);
        }

        public async Task UploadFolderAsync(string bucket, string localFolderPath, string remotePrefix = "")
        {
            if (!Directory.Exists(localFolderPath)) throw new DirectoryNotFoundException(localFolderPath);
            Log($"Uploading folder {localFolderPath} to s3://{bucket}/{remotePrefix}", verboseOnly: false);
            var files = Directory.GetFiles(localFolderPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(localFolderPath, file).Replace('\\', '/');
                var key = string.IsNullOrEmpty(remotePrefix) ? relative : (remotePrefix.TrimEnd('/') + "/" + relative);
                using var fs = File.OpenRead(file);
                await PutObjectAsync(bucket, key, fs, GetContentTypeByExtension(file), cancellationToken: default);
                Log($"Uploaded {file} => {key}", verboseOnly: true);
            }
            Log($"Completed uploading {files.Length} files", verboseOnly: false);
        }

        public async Task DownloadFolderAsync(string bucket, string prefix, string destinationFolder)
        {
            Log($"Downloading folder s3://{bucket}/{prefix} to {destinationFolder}", verboseOnly: false);
            var continuationToken = (string?)null;
            var total = 0;
            do
            {
                var req = new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = prefix ?? string.Empty,
                    ContinuationToken = continuationToken
                };
                var res = await _client.ListObjectsV2Async(req);
                foreach (var obj in res.S3Objects.Where(o => !o.Key.EndsWith("/")))
                {
                    var relative = (prefix ?? string.Empty).Length > 0 ? obj.Key.Substring((prefix ?? string.Empty).Length).TrimStart('/') : obj.Key;
                    var destPath = Path.Combine(destinationFolder, relative.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    await DownloadFileAsync(bucket, obj.Key, destPath, cancellationToken: default);
                    total++;
                }
                continuationToken = res.IsTruncated == true ? res.NextContinuationToken : null;
            } while (continuationToken != null);
            Log($"Downloaded {total} files", verboseOnly: false);
        }

        public async Task CopyObjectAsync(string sourceBucket, string sourceKey, string destinationBucket, string destinationKey)
        {
            Log($"Copying s3://{sourceBucket}/{sourceKey} to s3://{destinationBucket}/{destinationKey}", verboseOnly: false);
            var copyReq = new CopyObjectRequest
            {
                SourceBucket = sourceBucket,
                SourceKey = sourceKey,
                DestinationBucket = destinationBucket,
                DestinationKey = destinationKey
            };
            await _client.CopyObjectAsync(copyReq);
            Log($"Copy completed", verboseOnly: true);
        }

        public async Task RenameAsync(string bucket, string sourceKey, string destinationKey)
        {
            Log($"Renaming {sourceKey} => {destinationKey} in bucket {bucket}", verboseOnly: false);
            await CopyObjectAsync(bucket, sourceKey, bucket, destinationKey);
            await DeleteObjectsAsync(bucket, new List<string> { sourceKey });
            Log($"Rename completed", verboseOnly: true);
        }

        public async Task DeleteObjectsAsync(string bucket, List<string> keys)
        {
            Log($"Deleting {keys.Count} objects from {bucket}", verboseOnly: false);
            var deleteReq = new DeleteObjectsRequest
            {
                BucketName = bucket,
                Objects = keys.Select(k => new KeyVersion { Key = k }).ToList()
            };
            await _client.DeleteObjectsAsync(deleteReq);
            Log($"Delete completed", verboseOnly: true);
        }

        private static string GetContentTypeByExtension(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream",
            };
        }
    }
}
