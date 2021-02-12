// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using TestResultCoordinator.Reports;

    // Longhaul tests need to filter out recent results, so recent unmatched results
    // that will soon be matched don't result in false failures. Implementations will
    // clear out all expected results and the corresponding actual results up until
    // some configurable ignore threshold.
    public abstract class TestResultFilter
    {
        public async Task<(IAsyncEnumerable<TestOperationResult>, IAsyncEnumerable<TestOperationResult>)> FilterResults(TimeSpan unmatchedResultTolerance, IAsyncEnumerable<TestOperationResult> expectedTestResultsEnumerable, IAsyncEnumerable<TestOperationResult> actualTestResultsEnumerable)
        {
            List<TestOperationResult> expectedResults = await expectedTestResultsEnumerable.ToListAsync();
            List<TestOperationResult> actualResults = await actualTestResultsEnumerable.ToListAsync();
            DateTime startIgnoringAt = DateTime.UtcNow.Subtract(unmatchedResultTolerance);

            List<TestOperationResult> expectedResultsOutput = new List<TestOperationResult>();
            List<TestOperationResult> actualResultsOutput = new List<TestOperationResult>();
            int actualResultCounter = 0;
            for (int expectedResultCounter = 0; expectedResultCounter < expectedResults.Count - 1; expectedResultCounter++)
            {
                TestOperationResult expectedResult = expectedResults[expectedResultCounter];
                if (expectedResult.CreatedAt > startIgnoringAt)
                {
                    break;
                }

                Option<TestOperationResult> actualResult = Option.None<TestOperationResult>();
                if (actualResultCounter < actualResults.Count)
                {
                    actualResult = Option.Some(actualResults[actualResultCounter]);
                }

                bool hasHandledActualResult = this.FilterResultPair(expectedResult, actualResult, expectedResultsOutput, actualResultsOutput);
                if (hasHandledActualResult)
                {
                    actualResultCounter += 1;
                }
            }

            IAsyncEnumerable<TestOperationResult> expectedOutput = expectedResultsOutput.ToAsyncEnumerable<TestOperationResult>();
            IAsyncEnumerable<TestOperationResult> actualOutput = actualResultsOutput.ToAsyncEnumerable<TestOperationResult>();
            return (expectedOutput, actualOutput);
        }

        // This should behave as following:
        // The expected result is within the threshold, so record it.
        // If it is a success there will probably be an actual result
        // also. However it is also possible there isn't if the actual
        // source reporter has errors and doesn't report.
        //
        // Returns true if it has added the actual result.
        protected abstract bool FilterResultPair(TestOperationResult expectedResult, Option<TestOperationResult> actualResult, List<TestOperationResult> expectedResults, List<TestOperationResult> actualResults);
    }

    public class SimpleTestOperationResultFilter : TestResultFilter
    {
        SimpleTestOperationResultComparer comparer;

        public SimpleTestOperationResultFilter(SimpleTestOperationResultComparer comparer)
        {
            this.comparer = comparer;
        }

        protected override bool FilterResultPair(TestOperationResult expectedResult, Option<TestOperationResult> actualResult, List<TestOperationResult> expectedResults, List<TestOperationResult> actualResults)
        {
            expectedResults.Append(expectedResult);
            return actualResult.Match(
                actualResult =>
                {
                    if (this.comparer.Matches(expectedResult, actualResult))
                    {
                        actualResults.Append(actualResult);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                },
                () =>
                {
                    return false;
                });
        }
    }

    public class DirectMethodTestOperationResultFilter : TestResultFilter
    {
        public DirectMethodTestOperationResultFilter()
        {
        }

        protected override bool FilterResultPair(TestOperationResult expectedResult, Option<TestOperationResult> actualResult, List<TestOperationResult> expectedResults, List<TestOperationResult> actualResults)
        {
            expectedResults.Append(expectedResult);
            return actualResult.Match(
                actualResult =>
                {
                    DirectMethodTestResult dmSenderTestResult = JsonConvert.DeserializeObject<DirectMethodTestResult>(expectedResult.Result);
                    if ((int)dmSenderTestResult.HttpStatusCode == 200)
                    {
                        actualResults.Append(actualResult);
                    }

                    return true;
                },
                () =>
                {
                    return false;
                });
        }
    }
}
