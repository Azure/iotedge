// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class ReporterClientBase : IDisposable
    {
        ILogger logger;

        protected ReporterClientBase(ILogger logger)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public abstract void Dispose();

        internal static ReporterClientBase Create(
            ILogger logger,
            Option<Uri> reportingEndpointUrl,
            TransportType transportType)
        {
            if (reportingEndpointUrl.HasValue)
            {
                return new TestResultReporterClient(
                        reportingEndpointUrl.OrDefault(),
                        logger);
            }

            Preconditions.CheckNotNull(transportType, nameof(transportType));
            return new EventReporterClient(logger, transportType);
        }

        internal async Task SendTestResultAsync(TestResultBase testResult)
        {
            Preconditions.CheckNotNull(testResult, nameof(testResult));
            try
            {
                await this.ReportStatusAsync(testResult);
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Failed to send testResult");
            }
        }

        internal abstract Task ReportStatusAsync(TestResultBase testResult);
    }
}
