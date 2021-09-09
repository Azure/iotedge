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

            TimeSpan timeToExpire = certificates.ServerCertificate.NotAfter - DateTime.UtcNow;
            if (timeToExpire > TimeBuffer)
            {
                // Clamp the renew time to TimeSpan.FromMilliseconds(Int32.MaxValue)
                // This is the maximum value for the timer (~24 days)
                // Math.Min unfortunately doesn't work with TimeSpans so we need to do the check manually
                TimeSpan renewAfter = timeToExpire - (TimeBuffer / 2);
                TimeSpan clamped = renewAfter > MaxRenewAfter
                    ? MaxRenewAfter
                    : renewAfter;
                logger.LogInformation("Scheduling server certificate renewal for {0}.", DateTime.UtcNow.Add(renewAfter).ToString("o"));
                logger.LogDebug("Scheduling server certificate renewal timer for {0} (clamped to Int32.MaxValue).", DateTime.UtcNow.Add(clamped).ToString("o"));
                this.timer = new Timer(this.Callback, null, clamped, Timeout.InfiniteTimeSpan);
            }
            else
            {
                logger.LogWarning("Server certificate is expired ({0}). Not scheduling renewal.", timeToExpire.ToString("c"));
                this.timer = new Timer(this.Callback, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
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
            TimeSpan timeToExpire = this.certificates.ServerCertificate.NotAfter - DateTime.UtcNow;
            if (timeToExpire > TimeBuffer)
            {
                // Timer has expired but is not within the time window for renewal
                // Reschedule the timer.

                // Clamp the renew time to TimeSpan.FromMilliseconds(Int32.MaxValue)
                // This is the maximum value for the timer (~24 days)
                // Math.Min unfortunately doesn't work with TimeSpans so we need to do the check manually
                TimeSpan renewAfter = timeToExpire - (TimeBuffer / 2);
                TimeSpan clamped = renewAfter > MaxRenewAfter
                    ? MaxRenewAfter
                    : renewAfter;
                this.logger.LogDebug("Scheduling server certificate renewal timer for {0}.", DateTime.UtcNow.Add(clamped).ToString("o"));
                this.timer.Change(clamped, Timeout.InfiniteTimeSpan);
            }
            else
            {
                this.logger.LogInformation("Restarting process to perform server certificate renewal.");
                this.cts.Cancel();
                this.timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }
    }
}
