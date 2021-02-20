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

    // Longhaul tests need to filter out recent results, so recent unmatched results
    // that will soon be matched don't result in false failures. Implementations will
    // clear out all expected results and the corresponding actual results up until
    // some configurable ignore threshold.
    internal class TestResultFilter
    {

        ITestResultComparer<TestOperationResult> Comparer;

        internal TestResultFilter(ITestResultComparer<TestOperationResult> comparer)
        {
            this.Comparer = comparer;
        }

        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TestResultCoordinator));

        internal async Task<(IAsyncEnumerable<TestOperationResult>, IAsyncEnumerable<TestOperationResult>)> FilterResults(TimeSpan unmatchedResultTolerance, IAsyncEnumerable<TestOperationResult> expectedTestResultsEnumerable, IAsyncEnumerable<TestOperationResult> actualTestResultsEnumerable)
        {
            Logger.LogInformation("Filtering out recent results based on ignore threshold of {0}", unmatchedResultTolerance.ToString());

            List<TestOperationResult> expectedResults = await expectedTestResultsEnumerable.ToListAsync();
            List<TestOperationResult> actualResults = await actualTestResultsEnumerable.ToListAsync();
            DateTime startIgnoringAt = DateTime.UtcNow.Subtract(unmatchedResultTolerance);

            List<TestOperationResult> expectedResultsOutput = new List<TestOperationResult>();
            List<TestOperationResult> actualResultsOutput = new List<TestOperationResult>();
            int expectedResultCounter = 0;
            int actualResultCounter = 0;
            while (expectedResultCounter < expectedResults.Count && expectedResults[expectedResultCounter].CreatedAt < startIgnoringAt)
            {
                TestOperationResult expectedResult = expectedResults[expectedResultCounter];
                expectedResultsOutput.Add(expectedResult);
                expectedResultCounter += 1;

                if (actualResultCounter < actualResults.Count)
                {
                    // TODO: can be split into own func HandleActualResults
                    TestOperationResult actualResult = actualResults[actualResultCounter];
                    bool doResultsMatch = this.Comparer.Matches(expectedResult, actualResult);
                    if (doResultsMatch)
                    {
                        actualResultsOutput.Add(actualResult);
                        actualResultCounter += 1;

                        // get all duplicate actual results
                        while (actualResultCounter < actualResults.Count && this.Comparer.Matches(actualResult, actualResults[actualResultCounter]))
                        {
                            actualResultsOutput.Add(actualResults[actualResultCounter]);
                            actualResultCounter += 1;
                        }
                    }
                }
            }

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
