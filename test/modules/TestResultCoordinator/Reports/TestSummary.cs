// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    class TestSummary
    {
        internal TestSummary(SortedDictionary<string, string> testInfo, ITestResultReport[] testResultReports, string blobContainerUri)
        {
            this.TestInfo = Preconditions.CheckNotNull(testInfo, nameof(testInfo));
            this.TestResultReports = Preconditions.CheckNotNull(testResultReports, nameof(testResultReports));
            this.IsPassed = testResultReports.All(r => r.IsPassed);
            this.BlobContainerUri = blobContainerUri ?? string.Empty;
        }

        public SortedDictionary<string, string> TestInfo { get; }

        public bool IsPassed { get; }

        public ITestResultReport[] TestResultReports { get; }

        public string BlobContainerUri { get; }
    }
}
