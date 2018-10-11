// Copyright (c) Microsoft. All rights reserved.


namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CertificateRenewal : IDisposable
    {
        readonly CancellationTokenSource cts;

        public CancellationToken Token => this.cts.Token;

        public CertificateRenewal(EdgeHubCertificates certificates, ILogger logger)
        {
            Preconditions.CheckNotNull(certificates, nameof(certificates));
            Preconditions.CheckNotNull(logger, nameof(logger));

            TimeSpan timeToExpire = DateTime.UtcNow - certificates.ServerCertificate.NotAfter;
            if (timeToExpire > TimeSpan.Zero)
            {
                TimeSpan renewAfter = timeToExpire * Constants.CertificateRenewalPeriod;
                logger.LogInformation("Scheduling server certificate renewal for {}.", DateTime.UtcNow.Add(renewAfter).ToString("o"));
                this.cts = new CancellationTokenSource(renewAfter);
                this.cts.Token.Register(l => ((ILogger)l).LogInformation("Performing server certificate renewal."), logger);
            }
            else
            {
                this.cts = new CancellationTokenSource();
                logger.LogWarning("Time to server certificate expiration is {}. Not scheduling renewal.", timeToExpire.ToString("c"));
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
                this.cts.Dispose();
            }
        }
    }
}
