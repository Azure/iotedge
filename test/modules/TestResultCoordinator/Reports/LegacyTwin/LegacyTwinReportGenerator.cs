// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.LegacyTwin
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Reports;

    sealed class LegacyTwinReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(LegacyTwinReportGenerator));

        readonly string trackingId;

        internal LegacyTwinReportGenerator(
            string testDescription,
            string trackingId,
            string resultType,
            string senderSource,
            IAsyncEnumerator<TestOperationResult> senderTestResults)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.SenderTestResults = Preconditions.CheckNotNull(senderTestResults, nameof(senderTestResults));
        }

        internal string TestDescription { get; }

        internal string ResultType { get; }

        internal string SenderSource { get; }

        internal IAsyncEnumerator<TestOperationResult> SenderTestResults { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(LegacyTwinReportGenerator)} for Sources [{this.SenderSource}] ");
            IDictionary<int, int> results = new Dictionary<int, int>();
            bool isPassed = true;
            while (await this.SenderTestResults.MoveNextAsync())
            {
                int status = int.Parse(this.SenderTestResults.Current.Result.Substring(0, 3));
                if (status > 299)
                {
                    isPassed = false;
                }

                if (results.ContainsKey(status))
                {
                    results[status] = results[status] + 1;
                }
                else
                {
                    results[status] = 1;
                }
            }

            var report = new LegacyTwinReport(
                this.TestDescription,
                this.trackingId,
                this.ResultType,
                this.SenderSource,
                results,
                isPassed);

            Logger.LogInformation($"Successfully finished creating LegacyTwinReport for Source [{this.SenderSource}]");
            return report;
        }
    }
}
