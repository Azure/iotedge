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

        public static async Task<ReporterClientBase> CreateAsync(
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
            EventReporterClient eventReporterClient = new EventReporterClient(logger);
            return await eventReporterClient.InitAsync(transportType);
        }

        public async Task ReportStatus(TestResultBase report)
        {
            Preconditions.CheckNotNull(report, nameof(report));
            try
            {
                await this.ReportStatusAsync(report);
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Failed to send report");
            }
        }

        internal abstract Task ReportStatusAsync(TestResultBase report);
    }
}
