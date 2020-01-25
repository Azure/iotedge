// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a counting report to show test result counts, e.g. expect and match counts; and contains a list of unmatched test results.
    /// </summary>
    class EdgeHubRestartTestReport : TestResultReportBase
    {
        public EdgeHubRestartTestReport(
            string trackingId,
            string resultType
            )
            : base(trackingId, resultType)
        {
        }

        public override bool IsPassed => this.TotalExpectCount == this.TotalMatchCount;

        public override string Title => $"EdgeHubRestartTest Report between [{this.ExpectedSource}] and [{this.ActualSource}] ({this.ResultType})";
    }
}
