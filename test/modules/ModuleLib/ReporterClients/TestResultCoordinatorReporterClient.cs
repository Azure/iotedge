// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    using TestOperationResult = Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient.TestOperationResult;

    public sealed class TestResultCoordinatorReporterClient : ReporterClientBase
    {
        ILogger logger;
        string source;
        TestResultCoordinatorClient trcClient;

        private TestResultCoordinatorReporterClient(
            TestResultCoordinatorClient trcClient,
            ILogger logger,
            string source)
            : base(
                logger,
                source)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.trcClient = Preconditions.CheckNotNull(trcClient, nameof(trcClient));
        }

        public static TestResultCoordinatorReporterClient Create(Uri baseUri, ILogger logger, string source)
        {
            TestResultCoordinatorClient trcClient = new TestResultCoordinatorClient { BaseUrl = baseUri.AbsoluteUri };
            return new TestResultCoordinatorReporterClient(
                trcClient,
                logger,
                source);
        }

        public override void Dispose()
        {
            // Intentionall left blank
            // C# genereated from swagger.yaml automatically call Dispose()
        }

        // TODO: How to merge this between modules
        internal override async Task ReportStatusAsync(ReportContent report, string source)
        {
            Preconditions.CheckNotNull(report, nameof(report));
            Preconditions.CheckNonWhiteSpace(source, nameof(source));

            TestOperationResult stampedReport = report.GenerateReport() as Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient.TestOperationResult;
            stampedReport.Source = source;
            stampedReport.CreatedAt = DateTime.UtcNow;
            await this.trcClient.ReportResultAsync(stampedReport);
        }
    }
}