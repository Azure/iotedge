// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::TestResultCoordinator;
    using global::TestResultCoordinator.Reports;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class LongHaulFilteringTest
    {
        [Fact]
        public async Task FilterTestHappyPath()
        {
            TestableTestResultFilter filter = new TestableTestResultFilter(new SimpleTestOperationResultComparer(), this.Expected1, this.Actual1);
            TimeSpan unmatchedResultTolerance = TimeSpan.FromMinutes(5);
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, 10);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, 10);
            List<TestOperationResult> list = new List<TestOperationResult>();
            (var filteredExpectedTestResults, var filteredActualTestResults) =
                await filter.FilterResults(unmatchedResultTolerance, expectedResults, actualResults);
            Assert.Equal(2, await filteredExpectedTestResults.CountAsync());
            Assert.Equal(2, await filteredActualTestResults.CountAsync());
        }

        [Fact]
        public async Task FilterTestIncludeDuplicateActualResultsAndIgnoreResultsAfterTolerance()
        {
            TestableTestResultFilter filter = new TestableTestResultFilter(new SimpleTestOperationResultComparer(), this.Expected2, this.Actual2);
            TimeSpan unmatchedResultTolerance = TimeSpan.FromMinutes(5);
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, 10);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, 10);
            List<TestOperationResult> list = new List<TestOperationResult>();
            (var filteredExpectedTestResults, var filteredActualTestResults) =
                await filter.FilterResults(unmatchedResultTolerance, expectedResults, actualResults);
            Assert.Equal(3, await filteredExpectedTestResults.CountAsync());
            Assert.Equal(5, await filteredActualTestResults.CountAsync());
        }

        [Fact]
        public async Task FilterTestActualResultAtEndWithNoMatch()
        {
            TestableTestResultFilter filter = new TestableTestResultFilter(new SimpleTestOperationResultComparer(), this.Expected3, this.Actual3);
            TimeSpan unmatchedResultTolerance = TimeSpan.FromMinutes(5);
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, 10);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, 10);
            List<TestOperationResult> list = new List<TestOperationResult>();
            (var filteredExpectedTestResults, var filteredActualTestResults) =
                await filter.FilterResults(unmatchedResultTolerance, expectedResults, actualResults);
            Assert.Equal(3, await filteredExpectedTestResults.CountAsync());
            Assert.Equal(5, await filteredActualTestResults.CountAsync());
        }

        public Task<List<TestOperationResult>> Expected1(IAsyncEnumerable<TestOperationResult> enumerable)
        {
            var expectedResult1 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result1", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)));
            var expectedResult2 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result2", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(9)));
            var expectedResult3 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result3", DateTime.UtcNow);
            return Task.FromResult(new List<TestOperationResult> { expectedResult1, expectedResult2, expectedResult3 });
        }

        public Task<List<TestOperationResult>> Actual1(IAsyncEnumerable<TestOperationResult> enumerable)
        {
            var actualResult1 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result1", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)));
            var actualResult2 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result2", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(9)));
            return Task.FromResult(new List<TestOperationResult> { actualResult1, actualResult2 });
        }

        public Task<List<TestOperationResult>> Expected2(IAsyncEnumerable<TestOperationResult> enumerable)
        {
            var expectedResult1 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result1", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)));
            var expectedResult2 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result2", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(9)));
            var expectedResult3 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result3", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(8)));
            return Task.FromResult(new List<TestOperationResult> { expectedResult1, expectedResult2, expectedResult3 });
        }

        public Task<List<TestOperationResult>> Actual2(IAsyncEnumerable<TestOperationResult> enumerable)
        {
            var actualResult1 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result1", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)));
            var actualResult2 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result2", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(9)));
            var actualResult3 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result2", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(9)));
            var actualResult4 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result3", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(8)));
            // 5 Should get added. It's within tolerance, but it's a duplicate
            var actualResult5 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result3", DateTime.UtcNow);
            // 6 should get ignored
            var actualResult6 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result4", DateTime.UtcNow);
            return Task.FromResult(new List<TestOperationResult> { actualResult1, actualResult2, actualResult3, actualResult4, actualResult5, actualResult6 });
        }

        public Task<List<TestOperationResult>> Expected3(IAsyncEnumerable<TestOperationResult> enumerable)
        {
            var expectedResult1 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result1", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)));
            var expectedResult2 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result2", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(9)));
            var expectedResult3 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result3", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(8)));
            return Task.FromResult(new List<TestOperationResult> { expectedResult1, expectedResult2, expectedResult3 });
        }

        public Task<List<TestOperationResult>> Actual3(IAsyncEnumerable<TestOperationResult> enumerable)
        {
            var actualResult1 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result1", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)));
            var actualResult2 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result2", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(9)));
            var actualResult3 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result2", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(9)));
            var actualResult4 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result3", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(8)));
            var actualResult5 = new TestOperationResult("sender", TestOperationResultType.Messages.ToString(), "result4", DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(7)));
            return Task.FromResult(new List<TestOperationResult> { actualResult1, actualResult2, actualResult3, actualResult4, actualResult5 });
        }
    }

    internal class TestableTestResultFilter : TestResultFilter
    {
        // Using this class to override the ConvertToList function in the TestResultFilter.
        // This way, we can actually test the functionality of TestResultFilter, while entering our own test data
        Func<IAsyncEnumerable<TestOperationResult>, Task<List<TestOperationResult>>> convertToListMethod;
        Func<IAsyncEnumerable<TestOperationResult>, Task<List<TestOperationResult>>> convertToListMethod2;
        bool firstTime = true;
        public TestableTestResultFilter(
            ITestResultComparer<TestOperationResult> comparer,
            Func<IAsyncEnumerable<TestOperationResult>, Task<List<TestOperationResult>>> convertToListMethod,
            Func<IAsyncEnumerable<TestOperationResult>, Task<List<TestOperationResult>>> convertToListMethod2)
            : base(comparer)
        {
            this.convertToListMethod = convertToListMethod;
            this.convertToListMethod2 = convertToListMethod2;
        }

        protected override Task<List<TestOperationResult>> ConvertToList(IAsyncEnumerable<TestOperationResult> asyncEnumerable)
        {
            if (this.firstTime)
            {
                this.firstTime = false;
                return this.convertToListMethod(asyncEnumerable);
            }
            else
            {
                return this.convertToListMethod2(asyncEnumerable);
            }
        }
    }
}
