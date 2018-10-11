// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CertificateRenewal : IDisposable
    {
        readonly static TimeSpan TimeBuffer = TimeSpan.FromMinutes(5);
        readonly CancellationTokenSource cts;

        /// <summary>
        /// This cancellation token will expire when certificate renewal is required.
        /// </summary>
        public CancellationToken Token => this.cts.Token;

        public CertificateRenewal(EdgeHubCertificates certificates, ILogger logger)
        {
            Preconditions.CheckNotNull(certificates, nameof(certificates));
            Preconditions.CheckNotNull(logger, nameof(logger));

            TimeSpan timeToExpire = certificates.ServerCertificate.NotAfter - DateTime.UtcNow;
            if (timeToExpire > TimeBuffer)
            {
                var renewAfter = timeToExpire - TimeBuffer;
                logger.LogInformation("Scheduling server certificate renewal for {0}.", DateTime.UtcNow.Add(renewAfter).ToString("o"));
                this.cts = new CancellationTokenSource(renewAfter);
                this.cts.Token.Register(l => ((ILogger)l).LogInformation("Performing server certificate renewal."), logger);
            }
            else
            {
                this.cts = new CancellationTokenSource();
                logger.LogWarning("Server certificate is expired ({0}). Not scheduling renewal.", timeToExpire.ToString("c"));
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    this.cts.Dispose();
                }
                catch (OperationCanceledException)
                {
                    // ignore by design
                }
            }
        }
    }
}
