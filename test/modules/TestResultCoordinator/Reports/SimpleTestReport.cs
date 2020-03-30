// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is a simple report to list out all reported test results during test.
    /// Currently it is used for network controller report and error report.
    /// </summary>
    class SimpleTestReport : TestResultReportBase
    {
        public SimpleTestReport(
            string testDescription,
            string trackingId,
            string source,
            string resultType,
            IReadOnlyList<TestOperationResult> testResults)
            : base(testDescription, trackingId, resultType)
        {
            this.Source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.Results = Preconditions.CheckNotNull(testResults, nameof(testResults));
        }

        public string Source { get; }

        public IReadOnlyList<TestOperationResult> Results { get; }

        public override string Title => $"Simple Test Report for [{this.Source}] ({this.ResultType})";

        public override bool IsPassed => true;
    }
}
