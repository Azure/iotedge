// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a counting report to show deployment test result, e.g. expect and actual number of deployments.
    /// </summary>
    class DeploymentTestReport : TestResultReportBase
    {
        public DeploymentTestReport(
            string trackingId,
            string expectedSource,
            string actualSource,
            string resultType,
            ulong totalExpectedDeployments,
            ulong totalActualDeployments,
            ulong totalMatchedDeployments,
            Option<TestOperationResult> lastActualDeploymentTestResult,
            IReadOnlyList<TestOperationResult> unmatchedResults)
            : base(trackingId, resultType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.TotalExpectedDeployments = totalExpectedDeployments;
            this.TotalActualDeployments = totalActualDeployments;
            this.TotalMatchedDeployments = totalMatchedDeployments;
            this.LastActualDeploymentTestResult = lastActualDeploymentTestResult;
            this.UnmatchedResults = unmatchedResults ?? new List<TestOperationResult>();
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        public ulong TotalExpectedDeployments { get; }

        public ulong TotalActualDeployments { get; }

        public ulong TotalMatchedDeployments { get; }

        public Option<TestOperationResult> LastActualDeploymentTestResult { get; }

        public IReadOnlyList<TestOperationResult> UnmatchedResults { get; }

        public override bool IsPassed => this.TotalExpectedDeployments == this.TotalMatchedDeployments && this.TotalExpectedDeployments > 0;

        public override string Title => $"Deployment Test Report between [{this.ExpectedSource}] and [{this.ActualSource}] ({this.ResultType})";
    }
}
