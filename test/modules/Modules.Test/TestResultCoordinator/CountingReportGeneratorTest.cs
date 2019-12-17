// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::TestResultCoordinator;
    using global::TestResultCoordinator.Report;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Moq;
    using Xunit;

    public class CountingReportGeneratorTest
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            new List<object[]>
            {
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), Enumerable.Range(1, 7).Select(v => v.ToString()), 10, 7, 7, 0, 0 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "2", "3", "4", "5", "6" }, 10, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "6", "7" }, 10, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6", "7" }, 10, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "6", "7" }, 10, 7, 5, 0, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6" }, 10, 7, 5, 0, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "7" }, 10, 7, 5, 0, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "2", "2", "3", "4", "4", "5", "6" }, 10, 7, 6, 2, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "1", "2", "3", "4", "5", "6", "6" }, 10, 7, 6, 2, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "2", "3", "4", "5", "6" }, 4, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "6", "7" }, 4, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6", "7" }, 4, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "6", "7" }, 4, 7, 5, 0, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6" }, 4, 7, 5, 0, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "7" }, 4, 7, 5, 0, 2 },
            };

        [Fact]
        public void TestConstructorSuccess()
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();

            var reportGenerator = new CountingReportGenerator(
                Guid.NewGuid().ToString(),
                "expectedSource",
                mockExpectedStore.Object,
                "actualSource",
                mockActualStore.Object,
                "resultType1",
                new SimpleTestOperationResultComparer(),
                1000);

            Assert.Equal("actualSource", reportGenerator.ActualSource);
            Assert.Equal(mockActualStore.Object, reportGenerator.ActualStore);
            Assert.Equal("expectedSource", reportGenerator.ExpectedSource);
            Assert.Equal(mockExpectedStore.Object, reportGenerator.ExpectedStore);
            Assert.Equal("resultType1", reportGenerator.ResultType);
            Assert.Equal(typeof(SimpleTestOperationResultComparer), reportGenerator.TestResultComparer.GetType());
            Assert.Equal(1000, reportGenerator.BatchSize);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReportGenerator(
                    trackingId,
                    "expectedSource",
                    mockExpectedStore.Object,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    1000));

            Assert.StartsWith("trackingId", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenExpectedSourceIsNotProvided(string expectedSource)
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReportGenerator(
                    Guid.NewGuid().ToString(),
                    expectedSource,
                    mockExpectedStore.Object,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    1000));

            Assert.StartsWith("expectedSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenExpectedStoreIsNotProvided()
        {
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new CountingReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    null,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    1000));

            Assert.Equal("expectedStore", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenActualSourceIsNotProvided(string actualSource)
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedStore.Object,
                    actualSource,
                    mockActualStore.Object,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    1000));

            Assert.StartsWith("actualSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenActualStoreIsNotProvided()
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new CountingReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedStore.Object,
                    "actualSource",
                    null,
                    "resultType1",
                    new SimpleTestOperationResultComparer(),
                    1000));

            Assert.Equal("actualStore", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new CountingReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedStore.Object,
                    "actualSource",
                    mockActualStore.Object,
                    resultType,
                    new SimpleTestOperationResultComparer(),
                    1000));

            Assert.StartsWith("resultType", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenTestResultComparerIsNotProvided()
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new CountingReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedStore.Object,
                    "actualSource",
                    mockActualStore.Object,
                    "resultType1",
                    null,
                    1000));

            Assert.Equal("testResultComparer", ex.ParamName);
        }

        [Fact]
        public async Task TestCreateReportAsyncWithEmptyResults()
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();

            var reportGenerator = new CountingReportGenerator(
                Guid.NewGuid().ToString(),
                "expectedSource",
                mockExpectedStore.Object,
                "actualSource",
                mockActualStore.Object,
                "resultType1",
                new SimpleTestOperationResultComparer(),
                10);

            var report = (CountingReport<TestOperationResult>)await reportGenerator.CreateReportAsync();

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
            ulong expectedTotalDuplicateResultCount,
            int expectedMissingResultsCount)
        {
            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            string expectedSource = "expectedSource";
            string actualSource = "actualSource";
            string resultType = "resultType1";

            var reportGenerator = new CountingReportGenerator(
                Guid.NewGuid().ToString(),
                expectedSource,
                mockExpectedStore.Object,
                actualSource,
                mockActualStore.Object,
                resultType,
                new SimpleTestOperationResultComparer(),
                batchSize);

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

            var report = (CountingReport<TestOperationResult>)await reportGenerator.CreateReportAsync();

            Assert.Equal(expectedTotalExpectedCount, report.TotalExpectCount);
            Assert.Equal(expectedTotalMatchCount, report.TotalMatchCount);
            Assert.Equal(expectedTotalDuplicateResultCount, report.TotalDuplicateResultCount);
            Assert.Equal(expectedMissingResultsCount, report.UnmatchedResults.Count);
        }

        static List<(long, TestOperationResult)> GetStoreData(string source, string resultType, IEnumerable<string> resultValues, int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            foreach (string value in resultValues)
            {
                storeData.Add((count, new TestOperationResult(source, resultType, value, DateTime.UtcNow)));
                count++;
            }

            return storeData;
        }
    }
}
