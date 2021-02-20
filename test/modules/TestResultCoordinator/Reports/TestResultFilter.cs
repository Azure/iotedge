// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Reports;

    /// <summary>
    /// Longhaul tests need to filter out recent results, so recent unmatched results
    /// that will soon be matched don't result in false failures.
    ///
    /// This abstraction filters out any result with a CreatedAt
    /// within the configurable ignore threshold, whith
    /// the exception of actual results matching a
    /// non-filtered expected result.
    /// </summary>
    internal class TestResultFilter
    {
        ITestResultComparer<TestOperationResult> comparer;

        internal TestResultFilter(ITestResultComparer<TestOperationResult> comparer)
        {
            this.comparer = comparer;
        }

        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TestResultCoordinator));

        internal async Task<(IAsyncEnumerable<TestOperationResult>, IAsyncEnumerable<TestOperationResult>)> FilterResults(TimeSpan unmatchedResultTolerance, IAsyncEnumerable<TestOperationResult> expectedTestResultsEnumerable, IAsyncEnumerable<TestOperationResult> actualTestResultsEnumerable)
        {
            Logger.LogInformation("Filtering out recent results based on ignore threshold of {0}", unmatchedResultTolerance.ToString());

            List<TestOperationResult> expectedResults = await expectedTestResultsEnumerable.ToListAsync();
            List<TestOperationResult> actualResults = await actualTestResultsEnumerable.ToListAsync();
            List<TestOperationResult> expectedResultsOutput = new List<TestOperationResult>();
            List<TestOperationResult> actualResultsOutput = new List<TestOperationResult>();

            DateTime startIgnoringAt = DateTime.UtcNow.Subtract(unmatchedResultTolerance);

            int expectedResultCounter = 0;
            int actualResultCounter = 0;
            while (expectedResultCounter < expectedResults.Count && expectedResults[expectedResultCounter].CreatedAt < startIgnoringAt)
            {
                // add the expected result to output since it is within the ignore threshold
                TestOperationResult expectedResult = expectedResults[expectedResultCounter];
                expectedResultsOutput.Add(expectedResult);
                expectedResultCounter += 1;

                if (actualResultCounter < actualResults.Count)
                {
                    TestOperationResult actualResult = actualResults[actualResultCounter];
                    bool doResultsMatch = this.comparer.Matches(expectedResult, actualResult);
                    if (doResultsMatch)
                    {
                        // add the matching actual result to output regardless if it is within the ignore threshold
                        actualResultsOutput.Add(actualResult);
                        actualResultCounter += 1;

                        // get all duplicate actual results regardless if it is within the ignore threshold
                        while (actualResultCounter < actualResults.Count && this.comparer.Matches(actualResult, actualResults[actualResultCounter]))
                        {
                            actualResultsOutput.Add(actualResults[actualResultCounter]);
                            actualResultCounter += 1;
                        }
                    }
                }
            }

            // get all other actual results within the ignore threshold that have no matches 
            while (actualResultCounter < actualResults.Count && actualResults[actualResultCounter].CreatedAt < startIgnoringAt)
            {
                actualResultsOutput.Add(actualResults[actualResultCounter]);
                actualResultCounter += 1;
            }

            IAsyncEnumerable<TestOperationResult> expectedOutput = expectedResultsOutput.ToAsyncEnumerable<TestOperationResult>();
            IAsyncEnumerable<TestOperationResult> actualOutput = actualResultsOutput.ToAsyncEnumerable<TestOperationResult>();
            return (expectedOutput, actualOutput);
        }
    }
}
