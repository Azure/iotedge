// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System.Linq;

    class TestSummary
    {
        internal TestSummary(ITestResultReport[] testResultReports)
        {
            this.TestResultReports = testResultReports;
            this.IsPassed = testResultReports.All(r => r.IsPassed);
        }

        public bool IsPassed { get; }

        public ITestResultReport[] TestResultReports { get; }
    }
}
