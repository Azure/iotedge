// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    sealed class DeploymentTestReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DeploymentTestReportGenerator));

        readonly string trackingId;
        readonly ushort unmatchedResultsMaxSize;

        internal DeploymentTestReportGenerator(
            string testDescription,
            string trackingId,
            string expectedSource,
            IAsyncEnumerator<TestOperationResult> expectedTestResults,
            string actualSource,
            IAsyncEnumerator<TestOperationResult> actualTestResults,
            ushort unmatchedResultsMaxSize)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.trackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectedTestResults = Preconditions.CheckNotNull(expectedTestResults, nameof(expectedTestResults));
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ActualTestResults = Preconditions.CheckNotNull(actualTestResults, nameof(actualTestResults));
            this.unmatchedResultsMaxSize = Preconditions.CheckRange<ushort>(unmatchedResultsMaxSize, 1);

            this.TestResultComparer = new DeploymentTestResultComparer();
            this.ResultType = TestOperationResultType.Deployment.ToString();
        }

        internal string ActualSource { get; }

        internal IAsyncEnumerator<TestOperationResult> ActualTestResults { get; }

        internal string ExpectedSource { get; }

        internal IAsyncEnumerator<TestOperationResult> ExpectedTestResults { get; }

        internal string ResultType { get; }

        internal string TestDescription { get; }

        internal ITestResultComparer<TestOperationResult> TestResultComparer { get; }

        /// <summary>
        /// Compare 2 data stores and counting expected, actual, and matched results; and return a deployment test report.
        /// Actual deployment results can be less than expected deployment results.
        /// An actaul deployment test result is possible to use for verification of more than 1 expected deployment test result.
        /// It will log fail if actual store has more results than expect store.
        /// </summary>
        /// <returns>Test Result Report.</returns>
        public async Task<ITestResultReport> CreateReportAsync()
        {
            Logger.LogInformation($"Start to generate report by {nameof(DeploymentTestReportGenerator)} for Sources [{this.ExpectedSource}] and [{this.ActualSource}]");

            TestOperationResult lastActualDeploymentTestResult = null;
            ulong totalExpectedDeployments = 0;
            ulong totalActualDeployments = 0;
            ulong totalMatchedDeployments = 0;
            var unmatchedResults = new Queue<TestOperationResult>();

            bool hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
            if (hasExpectedResult)
            {
                totalExpectedDeployments++;
            }

            bool hasActualResult = await this.ActualTestResults.MoveNextAsync();
            if (hasActualResult)
            {
                lastActualDeploymentTestResult = this.ActualTestResults.Current;
                totalActualDeployments++;
            }

            while (hasExpectedResult && hasActualResult)
            {
                this.ValidateResult(this.ExpectedTestResults.Current, this.ExpectedSource);
                this.ValidateResult(this.ActualTestResults.Current, this.ActualSource);

                if (this.TestResultComparer.Matches(this.ExpectedTestResults.Current, this.ActualTestResults.Current))
                {
                    totalMatchedDeployments++;

                    hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                    if (hasExpectedResult)
                    {
                        totalExpectedDeployments++;
                    }
                }
                else
                {
                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                    if (hasActualResult)
                    {
                        totalActualDeployments++;
                        lastActualDeploymentTestResult = this.ActualTestResults.Current;
                    }
                }
            }

            while (hasExpectedResult)
            {
                TestReportUtil.EnqueueAndEnforceMaxSize(unmatchedResults, this.ExpectedTestResults.Current, this.unmatchedResultsMaxSize);
                hasExpectedResult = await this.ExpectedTestResults.MoveNextAsync();
                if (hasExpectedResult)
                {
                    totalExpectedDeployments++;
                }
            }

            hasActualResult = await this.ActualTestResults.MoveNextAsync();
            if (hasActualResult)
            {
                totalActualDeployments++;
                lastActualDeploymentTestResult = this.ActualTestResults.Current;

                while (hasActualResult)
                {
                    // Log message for unexpected case.
                    Logger.LogError($"[{nameof(DeploymentTestReportGenerator)}] Actual test result source has unexpected results.");

                    // Log actual queue items
                    Logger.LogError($"Unexpected actual test result: {this.ActualTestResults.Current.Source}, {this.ActualTestResults.Current.Type}, {this.ActualTestResults.Current.Result} at {this.ActualTestResults.Current.CreatedAt}");

                    hasActualResult = await this.ActualTestResults.MoveNextAsync();
                    if (hasActualResult)
                    {
                        lastActualDeploymentTestResult = this.ActualTestResults.Current;
                        totalActualDeployments++;
                    }
                }
            }

            return new DeploymentTestReport(
                this.TestDescription,
                this.trackingId,
                this.ExpectedSource,
                this.ActualSource,
                this.ResultType,
                totalExpectedDeployments,
                totalActualDeployments,
                totalMatchedDeployments,
                Option.Maybe(lastActualDeploymentTestResult),
                new List<TestOperationResult>(unmatchedResults).AsReadOnly());
        }

        void ValidateResult(TestOperationResult current, string expectedSource)
        {
            if (!current.Source.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Result source is '{current.Source}' but expected should be '{expectedSource}'.");
            }

            if (!current.Type.Equals(this.ResultType, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Result type is '{current.Type}' but expected should be '{this.ResultType}'.");
            }
        }
    }
}
