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
        readonly ILogger logger;
        readonly Timer timer;
        readonly CancellationTokenSource cts;

        /// <summary>
        /// This cancellation token will expire when certificate renewal is required.
        /// </summary>
        public CancellationToken Token => this.cts.Token;

        public CertificateRenewal(EdgeHubCertificates certificates, ILogger logger)
        {
            Preconditions.CheckNotNull(certificates, nameof(certificates));
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.cts = new CancellationTokenSource();

            TimeSpan timeToExpire = certificates.ServerCertificate.NotAfter - DateTime.UtcNow;
            if (timeToExpire > TimeBuffer)
            {
                var renewAfter = timeToExpire - TimeBuffer;
                logger.LogInformation("Scheduling server certificate renewal for {0}.", DateTime.UtcNow.Add(renewAfter).ToString("o"));
                this.timer = new Timer(this.Callback, null, renewAfter, Timeout.InfiniteTimeSpan);
            }
            else
            {
                logger.LogWarning("Server certificate is expired ({0}). Not scheduling renewal.", timeToExpire.ToString("c"));
                this.timer = new Timer(this.Callback, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Callback(object _state)
        {
            this.logger.LogInformation("Restarting process to perform server certificate renewal.");
            this.cts.Cancel();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    this.timer.Dispose();
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
