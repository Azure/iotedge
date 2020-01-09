// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.report.DirectMethodReport
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Report;
    using TestResultCoordinator.Report.DirectMethodReport;

    class DirectMethodReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DirectMethodReportGenerator));

        readonly string trackingId;

        public DirectMethodReportGenerator(
            string trackingId,
            string expectedSource,
            ITestResultCollection<TestOperationResult> expectedTestResults,
            string actualSource,
            ITestResultCollection<TestOperationResult> actualTestResults,
            string resultType,
            ITestResultComparer<TestOperationResult> testResultComparer)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ActualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
        }

        internal string ActualSource { get; }

        internal ITestResultCollection<TestOperationResult> ActualTestResults { get; }

        internal string ExpectedSource { get; }

        internal ITestResultCollection<TestOperationResult> ExpectedTestResults { get; }

        internal string ResultType { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        internal NetworkStatusTimeline NetworkStatusTimeline { get; }

        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(DirectMethodReportGenerator)} for Sources [{this.ExpectedSource}] and [{this.ActualSource}]");

            bool hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            bool hasActualResult = await this.ActualTestResults.MoveNextAsync();
            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateDataSource(this.ExpectedTestResults.Current, this.ExpectedSource);
                this.ValidateDataSource(this.ActualTestResults.Current, this.ActualSource);

                if (this.TestResultComparer.Matches(this.ExpectedTestResults.Current, this.ActualTestResults.Current))
                {
                    // Found same message in both stores.
                    // If network off at time, fail.
                    // If network on at time, succeed

                }

            }
        }
        void ValidateDataSource(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected should be '{expectedSource}'.");
            }
        }
    }
}
