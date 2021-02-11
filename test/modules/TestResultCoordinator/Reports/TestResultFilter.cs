// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
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

        public (IAsyncEnumerable<TestOperationResult>, IAsyncEnumerable<TestOperationResult>) FilterResults(IAsyncEnumerable<TestOperationResult> expectedTestResults, IAsyncEnumerable<TestOperationResult> actualTestResults)
        {
            // TODO: filter unmatched expected results up to some tolerance
            return (expectedTestResults, actualTestResults);
        }
    }
}
