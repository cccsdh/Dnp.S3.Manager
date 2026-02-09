// -----------------------------------------------------------------------
// <copyright file="S3ClientFactory.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Dnp.S3.Manager.Lib
{
    public interface IS3ClientFactory
    {
        S3Client Create(string accessKey, string secretKey, string region);
    }

    public class S3ClientFactory : IS3ClientFactory
    {
        private readonly LogVerbosity _verbosity;
        private readonly ILogger? _logger;

        public S3ClientFactory() : this(LogVerbosity.Terse, null) { }
        public S3ClientFactory(Microsoft.Extensions.Logging.ILogger<S3ClientFactory> logger) : this(LogVerbosity.Terse, logger) { }
        public S3ClientFactory(LogVerbosity verbosity, ILogger? logger = null)
        {
            _verbosity = verbosity;
            _logger = logger;
        }

        public S3Client Create(string accessKey, string secretKey, string region)
        {
            return S3Client.CreateWithAws(accessKey, secretKey, region, _verbosity, _logger);
        }
    }
}
