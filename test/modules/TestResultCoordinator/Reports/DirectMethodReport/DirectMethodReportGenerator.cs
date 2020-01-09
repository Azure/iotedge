// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethodReport
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Report.DirectMethodReport;
    using TestResultCoordinator.Reports;

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
            ITestResultComparer<TestOperationResult> testResultComparer,
            NetworkStatusTimeline networkStatusTimeline)
        {
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ActualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.TestResultComparer = Preconditions.CheckNotNull(testResultComparer, nameof(testResultComparer));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
            this.NetworkStatusTimeline = Preconditions.CheckNotNull(networkStatusTimeline, nameof(networkStatusTimeline));
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

            ulong networkOnSuccess = 0;
            ulong networkOffSuccess = 0;
            ulong networkOnToleratedSuccess = 0;
            ulong networkOffToleratedSuccess = 0;
            ulong networkOnFailure = 0;
            ulong networkOffFailure = 0;
            ulong mismatchSuccess = 0;
            ulong mismatchFailure = 0;

        bool hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            bool hasActualResult = await this.ActualTestResults.MoveNextAsync();

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateDataSource(this.ExpectedTestResults.Current, this.ExpectedSource);
                this.ValidateDataSource(this.ActualTestResults.Current, this.ActualSource);

                if (this.TestResultComparer.Matches(this.ExpectedTestResults.Current, this.ActualTestResults.Current))
                {
                    // Found same message in both stores.
                    (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod) = this.NetworkStatusTimeline.GetNetworkControllerStatusAndWithinToleranceAt(this.ExpectedTestResults.Current.CreatedAt);

                    if (NetworkControllerStatus.Disabled.Equals(networkControllerStatus))
                    {
                        // If network on at time, succeed
                        networkOnSuccess++;
                    }
                    if (NetworkControllerStatus.Enabled.Equals(networkControllerStatus))
                    {
                        // If network off at time, fail unless in tolerance period
                        if (isWithinTolerancePeriod)
                        {
                            networkOffToleratedSuccess++;
                        }

                        networkOffFailure++;
                    }
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
