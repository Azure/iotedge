// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::TestResultCoordinator.Reports;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class TwinCountingReportGeneratorTest
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            new List<object[]>
            {
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), Enumerable.Range(1, 7).Select(v => v.ToString()), 10, 7, 7, 7, 0 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "2", "3", "4", "5", "6" }, 10, 7, 6, 6, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "6", "7" }, 10, 7, 6, 6, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6", "7" }, 10, 7, 6, 6, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "6", "7" }, 10, 7, 5, 5, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6" }, 10, 7, 5, 5, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "7" }, 10, 7, 5, 5, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1;2;3", "4", "5", "6" }, 10, 7, 6, 4, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "2;3", "4", "5", "6" }, 10, 7, 6, 5, 1 },
                new object[] { new[] { "1;2;3;4;5", "6" }, new[] { "1", "2", "3", "4", "5", "6" }, 10, 6, 6, 6, 0 },
                new object[] { new[] { "1;2;3;4;5", "6" }, new[] { "1", "2", "3;4;5", "6" }, 10, 6, 6, 4, 0 },
                new object[] { new[] { "1;2;3;4;5", "6" }, new[] { "1", "2", "5;4", "6" }, 10, 6, 5, 4, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "2", "3", "4", "5", "6" }, 4, 7, 6, 6, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "6", "7" }, 4, 7, 6, 6, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6", "7" }, 4, 7, 6, 6, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "6", "7" }, 4, 7, 5, 5, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6" }, 4, 7, 5, 5, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "7" }, 4, 7, 5, 5, 2 },
            };
        public static readonly string TestDescription = "dummy description";

        static readonly ushort UnmatchedResultsMaxSize = 10;

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTestDescriptionIsNotProvided(string testDescription)
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new TwinCountingReportGenerator(
                    testDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    UnmatchedResultsMaxSize));

            Assert.StartsWith("testDescription", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new TwinCountingReportGenerator(
                    TestDescription,
                    trackingId,
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    UnmatchedResultsMaxSize));

            Assert.StartsWith("trackingId", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenExpectedSourceIsNotProvided(string expectedSource)
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new TwinCountingReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    expectedSource,
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    UnmatchedResultsMaxSize));

            Assert.StartsWith("expectedSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenExpectedStoreIsNotProvided()
        {
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new TwinCountingReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    null,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    UnmatchedResultsMaxSize));

            Assert.Equal("expectedTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenActualSourceIsNotProvided(string actualSource)
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new TwinCountingReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    actualSource,
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    UnmatchedResultsMaxSize));

            Assert.StartsWith("actualSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenActualStoreIsNotProvided()
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new TwinCountingReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    null,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    UnmatchedResultsMaxSize));

            Assert.Equal("actualTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
                    resultType,
                    new SimpleTestOperationResultComparer(),
                    UnmatchedResultsMaxSize,
                    false));

            Assert.StartsWith("resultType", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenTestResultComparerIsNotProvided()
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new TwinCountingReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    null,
                    UnmatchedResultsMaxSize));

            Assert.Equal("testResultComparer", ex.ParamName);
        }

        [Theory]
        [InlineData(0)]
        public void TestConstructorThrowsWhenUnmatchedResultsMaxSizeIsNonPositive(ushort unmatchedResultsMaxSize)
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new TwinCountingReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    unmatchedResultsMaxSize));
        }

        [Fact]
        public async Task TestCreateReportAsyncWithEmptyResults()
        {
            string expectedSource = "expectedSource";
            string actualSource = "actualSource";
            int batchSize = 10;

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, batchSize);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);

            var reportGenerator = new CountingReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                expectedSource,
                expectedResults.GetAsyncEnumerator(),
                actualSource,
                actualResults.GetAsyncEnumerator(),
                "resultType1",
                new SimpleTestOperationResultComparer(),
                UnmatchedResultsMaxSize,
                false);

            var report = (CountingReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(0UL, report.TotalExpectCount);
            Assert.Equal(0UL, report.TotalMatchCount);
            Assert.Equal(0UL, report.TotalDuplicateResultCount);
            Assert.Equal(0, report.UnmatchedResults.Count);
        }

        [Theory]
        [MemberData(nameof(GetCreateReportData))]
        public async Task TestCreateReportAsync(
            IEnumerable<string> expectedStoreValues,
            IEnumerable<string> actualStoreValues,
            int batchSize,
            ulong expectedTotalExpectedCount,
            ulong expectedTotalMatchCount,
            ulong expectedTotalPatches,
            int expectedMissingResultsCount)
        {
            string expectedSource = "expectedSource";
            string actualSource = "actualSource";
            string resultType = "resultType1";

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, batchSize);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);

            var reportGenerator = new TwinCountingReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                expectedSource,
                expectedResults.GetAsyncEnumerator(),
                actualSource,
                actualResults.GetAsyncEnumerator(),
                resultType,
                new SimpleTestOperationResultComparer(),
                UnmatchedResultsMaxSize);

            var expectedStoreData = GetStoreData(expectedSource, resultType, expectedStoreValues);
            for (int i = 0; i < expectedStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockExpectedStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(expectedStoreData.Skip(startingOffset).Take(batchSize));
            }

            var actualStoreData = GetStoreData(actualSource, resultType, actualStoreValues);
            for (int j = 0; j < expectedStoreData.Count; j += batchSize)
            {
                int startingOffset = j;
                mockActualStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(actualStoreData.Skip(startingOffset).Take(batchSize));
            }

            var report = (TwinCountingReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(expectedTotalExpectedCount, report.TotalExpectCount);
            Assert.Equal(expectedTotalMatchCount, report.TotalMatchCount);
            Assert.Equal(expectedTotalPatches, report.TotalPatchesCount);
            Assert.Equal(expectedMissingResultsCount, report.UnmatchedResults.Count);
        }

        static List<(long, TestOperationResult)> GetStoreData(string source, string resultType, IEnumerable<string> resultValues, int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            foreach (string value in resultValues)
            {
                var tc = new Microsoft.Azure.Devices.Shared.TwinCollection();
                var values = value.Split(";");
                foreach (var item in values)
                {
                    tc[item] = "1";
                }

                var twinTestResult = new TwinTestResult(source, DateTime.UtcNow) { Properties = tc };
                storeData.Add((count, twinTestResult.ToTestOperationResult()));
                count++;
            }

            return storeData;
        }
    }
}
