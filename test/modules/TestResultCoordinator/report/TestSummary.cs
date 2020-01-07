// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System.Linq;

    class TestSummary
    {
        internal TestSummary(ITestResultReport[] testResultReports)
        {
            this.TestResultReports = testResultReports;
            this.AreAllTestsPassed = testResultReports.All(r => r.IsPassed);
        }

        public bool AreAllTestsPassed { get; private set; }

        public ITestResultReport[] TestResultReports { get; private set; }
    }
}
