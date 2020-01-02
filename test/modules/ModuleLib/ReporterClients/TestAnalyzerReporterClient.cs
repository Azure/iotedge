// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.ReporterClients
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    using TestOperationResult = Microsoft.Azure.Devices.Edge.ModuleUtil.TestOperationResult;

    // TODO: Merge Philips' change and this class can be merged with TRCReportCLient
    public sealed class TestAnalyzerReporterClient : ReporterClientBase
    {
        ILogger logger;
        string source;
        AnalyzerClient analyzerClient;

        private TestAnalyzerReporterClient(
            AnalyzerClient analyzerClient,
            ILogger logger,
            string source)
            : base(
                logger,
                source)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.analyzerClient = Preconditions.CheckNotNull(analyzerClient, nameof(analyzerClient));
        }

        public static TestAnalyzerReporterClient Create(Uri baseUri, ILogger logger, string source)
        {
            AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = baseUri.AbsoluteUri };
            return new TestAnalyzerReporterClient(
                analyzerClient,
                logger,
                source);
        }

        public override void Dispose()
        {
            // Intentionall left blank
            // C# genereated from swagger.yaml automatically call Dispose()
        }

        internal override async Task ReportStatusAsync(ReportContent report, string source)
        {
            Preconditions.CheckNotNull(report, nameof(report));
            Preconditions.CheckNonWhiteSpace(source, nameof(source));

            TestOperationResult stampedReport = report.GenerateReport(TestOperationResultType.LegacyDirectMethod) as Microsoft.Azure.Devices.Edge.ModuleUtil.TestOperationResult;
            stampedReport.Source = source;
            stampedReport.CreatedAt = DateTime.UtcNow;
            await this.analyzerClient.ReportResultAsync(stampedReport);
        }
    }
}