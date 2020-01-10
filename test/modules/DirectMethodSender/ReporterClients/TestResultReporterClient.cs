// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    sealed class TestResultReporterClient : ReporterClientBase
    {
        readonly ILogger logger;
        readonly Uri baseUri;
        readonly TestResultReportingClient testResultReportingClient = null;

        internal TestResultReporterClient(Uri baseUri, ILogger logger)
            : base(logger)
        {
            Preconditions.CheckNotNull(baseUri, nameof(baseUri));
            this.baseUri = baseUri;
            this.testResultReportingClient = new TestResultReportingClient { BaseUrl = baseUri.AbsoluteUri };
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public override void Dispose()
        {
            // Intentionall left blank
            // C# genereated from swagger.yaml automatically call Dispose()
        }

        internal override async Task ReportStatusAsync(TestResultBase testResult)
        {
            Preconditions.CheckNotNull(testResult, nameof(testResult));
            await this.testResultReportingClient.ReportResultAsync(testResult.ToTestOperationResultDto());
        }
    }
}
