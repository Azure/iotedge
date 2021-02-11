// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    public class TestResultFilter
    {
        TimeSpan unmatchedResultTolerance;

        public TestResultFilter(TimeSpan unmatchedResultTolerance)
        {
            this.unmatchedResultTolerance = unmatchedResultTolerance;
        }

        public (ITestResultCollection<TestOperationResult>, ITestResultCollection<TestOperationResult>) FilterResults(ITestResultCollection<TestOperationResult> expectedTestResults, ITestResultCollection<TestOperationResult> actualTestResults)
        {
            return (expectedTestResults, actualTestResults);
        }
    }
}
