// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
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

        // TODO: Change this create signature once the TRC and Analyzer URL is merged
        public static async Task<ReporterClientBase> CreateAsync(
            ILogger logger,
            Option<Uri> testResultCoordinatorUrl,
            Option<Uri> analyzerUrl,
            TransportType transportType)
        {
            if (testResultCoordinatorUrl.HasValue)
            {
                return new TestResultReporterClient(
                        testResultCoordinatorUrl.Expect(() => new ArgumentException("testReportCoordinatorUrl is not expected to be empty")),
                        logger);
            }
            else if (analyzerUrl.HasValue)
            {
                return new TestResultReporterClient(
                        analyzerUrl.Expect(() => new ArgumentException("analyzerUrl is not expected to be empty")),
                        logger);
            }
            else
            {
                Preconditions.CheckNotNull(transportType, nameof(transportType));
                EventReporterClient eventReporterClient = new EventReporterClient(logger);
                return await eventReporterClient.InitAsync(transportType);
            }
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
                this.logger.LogError(e.ToString());
            }
        }

        internal abstract Task ReportStatusAsync(TestResultBase report);
    }
}
