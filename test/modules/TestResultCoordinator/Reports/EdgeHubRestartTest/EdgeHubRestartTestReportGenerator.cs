// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    sealed class EdgeHubRestartTestReportGenerator : ITestResultReportGenerator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(EdgeHubRestartTestReportGenerator));

        internal EdgeHubRestartTestReportGenerator(
            string trackingId,
            string restarterSource,
            ITestResultCollection<TestOperationResult> restartResults,
            ITestResultReport attachedTestReport,
            TimeSpan passableEdgeHubRestartPeriod)
        {
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.RestarterSource = Preconditions.CheckNonWhiteSpace(restarterSource, nameof(restarterSource));
            this.RestartResults = Preconditions.CheckNotNull(restartResults);
            this.AttachedTestReport = Preconditions.CheckNotNull(attachedTestReport);
            this.PassableEdgeHubRestartPeriod = passableEdgeHubRestartPeriod;
        }

        internal string TrackingId { get; }

        internal string RestarterSource { get; }

        internal ITestResultCollection<TestOperationResult> RestartResults { get; }

        internal ITestResultReport AttachedTestReport { get; }

        internal TimeSpan PassableEdgeHubRestartPeriod { get; }

        public Task<ITestResultReport> CreateReportAsync()
        {
            // Verify the result that is passed in to make sure it passed.
            bool isUnderlyingTestPassed = this.AttachedTestReport.IsPassed;

            // Verify the the restarting time is less than the treshold
            return new EdgeHubRestartTestReport(
                this.trackingId,
                this.ExpectedSource,
                this.ActualSource,
                this.ResultType,
                totalExpectCount,
                totalMatchCount,
                totalDuplicateResultCount,
                unmatchedResults.AsReadOnly());
        }
    }


}