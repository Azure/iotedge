// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Linq;

    class TestSummary
    {
        internal TestSummary(ITestResultReport[] testResultReports, string blobContainerUri)
        {
            this.TestResultReports = testResultReports;
            this.IsPassed = testResultReports.All(r => r.IsPassed);
            this.BlobContainerUri = blobContainerUri ?? string.Empty;
        }

        public bool IsPassed { get; }

        public ITestResultReport[] TestResultReports { get; }

        public string BlobContainerUri { get; }
    }
}
