// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
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
        public ReporterClientBase Create(
            FrameworkTestType testType,
            ILogger logger,
            Option<Uri> testResultCoordinatorUrl,
            Option<Uri> analyzerUrl,
            TransportType transportType)
        {
            switch (testType)
            {
                case FrameworkTestType.Connectivity:
                    return TestResultCoordinatorReporterClient.Create(
                        testResultCoordinatorUrl.Expect(() => new ArgumentException("testReportCoordinatorUrl is not expected to be empty")),
                        logger);

                case FrameworkTestType.LongHaul:
                    return TestResultCoordinatorReporterClient.Create(
                        analyzerUrl.Expect(() => new ArgumentException("analyzerUrl is not expected to be empty")),
                        logger);

                case FrameworkTestType.EndToEnd:
                default:
                    return ModuleReporterClient.Create(
                        transportType,
                        logger);
            }
        }

        public async Task ReportStatus(ReportContent report)
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

        internal abstract Task ReportStatusAsync(ReportContent report);
    }
}