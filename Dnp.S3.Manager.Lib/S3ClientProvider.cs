// -----------------------------------------------------------------------
// <copyright file="S3ClientProvider.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Dnp.S3.Manager.Lib
{
    public interface IS3ClientProvider
    {
        S3Client? Current { get; }
        void SetClient(S3Client client);
    }

    public class S3ClientProvider : IS3ClientProvider
    {
        private S3Client? _client;
        public S3Client? Current => _client;
        public void SetClient(S3Client client) => _client = client;
    }
}
