using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Amazon.S3.Model;
using Amazon.S3;
using Amazon.S3.Transfer;
using Dnp.S3.Manager.Lib;

namespace Dnp.S3.Manager.Lib.Tests
{
    [TestClass]
    public class S3ClientTests
    {
        [TestMethod]
        public async Task ListBucketsAsync_Returns_Bucket_Names()
        {
            var mockS3 = new Mock<S3Client.IS3Api>();
            var resp = new ListBucketsResponse();
            resp.Buckets.Add(new S3Bucket { BucketName = "b1" });
            resp.Buckets.Add(new S3Bucket { BucketName = "b2" });
            mockS3.Setup(c => c.ListBucketsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(resp);

            var mockTU = new Mock<S3Client.ITransferUtility>();
            var client = new S3Client(mockS3.Object, mockTU.Object);

            var list = await client.ListBucketsAsync();
            CollectionAssert.AreEqual(new[] { "b1", "b2" }, list);
        }

        [TestMethod]
        public async Task ListObjectsAsync_Returns_Folders_And_Files()
        {
            var mockS3 = new Mock<S3Client.IS3Api>();
            var resp = new ListObjectsV2Response();
            resp.CommonPrefixes.Add("folder1/");
            resp.S3Objects.Add(new S3Object { Key = "file1.txt", Size = 123, LastModified = DateTime.UtcNow });
            mockS3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(resp);

            var mockTU = new Mock<S3Client.ITransferUtility>();
            var client = new S3Client(mockS3.Object, mockTU.Object);

            var result = await client.ListObjectsAsync("bucket");
            Assert.AreEqual(1, result.Folders.Count);
            Assert.AreEqual(1, result.Files.Count);
            Assert.AreEqual("file1.txt", result.Files[0].Key);
        }

        [TestMethod]
        public async Task PutObjectAsync_Invokes_TransferUtility_And_Reports_Progress()
        {
            var mockS3 = new Mock<S3Client.IS3Api>();
            var mockTU = new Mock<S3Client.ITransferUtility>();

            // Simple UploadAsync mock that completes; S3Client reports 100% after upload completes
            mockTU.Setup(t => t.UploadAsync(It.IsAny<TransferUtilityUploadRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var client = new S3Client(mockS3.Object, mockTU.Object);

            double last = 0;
            var progress = new Progress<double>(p => last = p);
            using var ms = new MemoryStream(new byte[10]);
            // disambiguate overload by providing contentType argument
            await client.PutObjectAsync("bucket", "key", ms, "application/octet-stream", progress);

            // S3Client reports 100% after UploadAsync completes
            Assert.AreEqual(100.0, last, 0.001);
            mockTU.Verify(t => t.UploadAsync(It.IsAny<TransferUtilityUploadRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetObjectAsync_Returns_Response()
        {
            var mockS3 = new Mock<S3Client.IS3Api>();
            var resp = new GetObjectResponse();
            resp.ResponseStream = new MemoryStream(new byte[] { 1, 2, 3 });
            resp.ContentLength = 3;
            mockS3.Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(resp);

            var mockTU = new Mock<S3Client.ITransferUtility>();
            var client = new S3Client(mockS3.Object, mockTU.Object);

            var r = await client.GetObjectAsync("b", "k");
            Assert.IsNotNull(r);
            Assert.AreEqual(3, r.ContentLength);
        }

        [TestMethod]
        public async Task DownloadFileAsync_Writes_File_And_Reports_Progress()
        {
            var mockS3 = new Mock<S3Client.IS3Api>();
            var content = new byte[5] { 1, 2, 3, 4, 5 };
            var resp = new GetObjectResponse { ResponseStream = new MemoryStream(content), ContentLength = content.Length };
            mockS3.Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(resp);

            var mockTU = new Mock<S3Client.ITransferUtility>();
            var client = new S3Client(mockS3.Object, mockTU.Object);

            var tmp = Path.GetTempFileName();
            try
            {
                double last = 0;
                var progress = new Progress<double>(p => last = p);
                await client.DownloadFileAsync("b", "k", tmp, progress);
                Assert.AreEqual(100.0, last, 0.001);
                var read = File.ReadAllBytes(tmp);
                CollectionAssert.AreEqual(content, read);
            }
            finally { File.Delete(tmp); }
        }

        [TestMethod]
        public async Task UploadFolderAsync_Uploads_All_Files()
        {
            var mockS3 = new Mock<S3Client.IS3Api>();
            var mockTU = new Mock<S3Client.ITransferUtility>();
            mockTU.Setup(t => t.UploadAsync(It.IsAny<TransferUtilityUploadRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var client = new S3Client(mockS3.Object, mockTU.Object);

            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            try
            {
                var f1 = Path.Combine(dir, "a.txt");
                var f2 = Path.Combine(dir, "sub", "b.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(f2)!);
                File.WriteAllText(f1, "one");
                File.WriteAllText(f2, "two");

                await client.UploadFolderAsync("bucket", dir, "prefix");

                mockTU.Verify(t => t.UploadAsync(It.IsAny<TransferUtilityUploadRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
            finally { Directory.Delete(dir, true); }
        }

        [TestMethod]
        public async Task DownloadFolderAsync_Downloads_Objects()
        {
            var mockS3 = new Mock<S3Client.IS3Api>();
            var listResp = new ListObjectsV2Response();
            listResp.S3Objects.Add(new S3Object { Key = "p/file1.txt" });
            mockS3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(listResp);

            var content = new byte[] { 9, 8, 7 };
            var getResp = new GetObjectResponse { ResponseStream = new MemoryStream(content), ContentLength = content.Length };
            mockS3.Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(getResp);

            var mockTU = new Mock<S3Client.ITransferUtility>();
            var client = new S3Client(mockS3.Object, mockTU.Object);

            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            try
            {
                await client.DownloadFolderAsync("bucket", "p/", dir);
                var file = Path.Combine(dir, "file1.txt");
                Assert.IsTrue(File.Exists(file));
                CollectionAssert.AreEqual(content, File.ReadAllBytes(file));
            }
            finally { Directory.Delete(dir, true); }
        }

        [TestMethod]
        public async Task Copy_Delete_Rename_Invoke_S3_Client()
        {
            var mockS3 = new Mock<S3Client.IS3Api>();
            mockS3.Setup(c => c.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CopyObjectResponse()).Verifiable();
            mockS3.Setup(c => c.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new DeleteObjectsResponse()).Verifiable();

            var mockTU = new Mock<S3Client.ITransferUtility>();
            var client = new S3Client(mockS3.Object, mockTU.Object);

            await client.CopyObjectAsync("sb", "sk", "db", "dk");
            mockS3.Verify(c => c.CopyObjectAsync(It.Is<CopyObjectRequest>(r => r.SourceBucket == "sb" && r.DestinationBucket == "db"), It.IsAny<CancellationToken>()), Times.Once);

            await client.DeleteObjectsAsync("b", new System.Collections.Generic.List<string> { "k1", "k2" });
            mockS3.Verify(c => c.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), It.IsAny<CancellationToken>()), Times.Once);

            await client.RenameAsync("b", "old", "new");
            mockS3.Verify(c => c.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            mockS3.Verify(c => c.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
    }
}
