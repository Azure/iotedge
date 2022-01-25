// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CertificateRenewal : IDisposable
    {
        static readonly TimeSpan MaxRenewAfter = TimeSpan.FromMilliseconds(int.MaxValue);
        static readonly TimeSpan TimeBuffer = TimeSpan.FromMinutes(5);

        readonly EdgeHubCertificates certificates;
        readonly ILogger logger;
        readonly Timer timer;
        readonly CancellationTokenSource cts;

        public CertificateRenewal(EdgeHubCertificates certificates, ILogger logger)
        {
            this.certificates = Preconditions.CheckNotNull(certificates, nameof(certificates));
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.cts = new CancellationTokenSource();

            TimeSpan timeToExpire = TimeSpan.FromMinutes(10);
            this.timer = new Timer(this.Callback, null, timeToExpire, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// This cancellation token will expire when certificate renewal is required.
        /// </summary>
        public CancellationToken Token => this.cts.Token;

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
                    this.timer.Dispose();
                    this.cts.Dispose();
                }
                catch (OperationCanceledException)
                {
                    // ignore by design
                }
            }
        }

        void Callback(object _state)
        {
            this.cts.Cancel();
        }
    }
}
