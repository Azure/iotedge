// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public sealed class TestResultCoordinatorReporterClient : ReporterClientBase
    {
        ILogger logger;
        TestResultReportingClient trcrClient;

        internal TestResultCoordinatorReporterClient(Uri baseUri, ILogger logger)
            : base(logger)
        {
            Preconditions.CheckNotNull(baseUri, nameof(baseUri));
            this.trcrClient = new TestResultReportingClient { BaseUrl = baseUri.AbsoluteUri };
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public override void Dispose()
        {
            // Intentionall left blank
            // C# genereated from swagger.yaml automatically call Dispose()
        }

        internal override async Task ReportStatusAsync(TestResultBase report)
        {
            Preconditions.CheckNotNull(report, nameof(report));
            //BEARWASHERE -- TEST
            this.logger.LogInformation($"Sending test result - TRC: Source={report.Source}, Type={report.ResultType}, CreatedAt={report.CreatedAt}");
            await this.trcrClient.ReportResultAsync(report.ToTestOperationResultDto());
        }
    }
}
