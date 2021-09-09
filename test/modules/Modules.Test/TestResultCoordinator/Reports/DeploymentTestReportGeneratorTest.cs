// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::TestResultCoordinator.Reports;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DeploymentTestReportGeneratorTest
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            new List<object[]>
            {
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 7, 7, 0, null },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), Enumerable.Range(1, 6).Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 6, 6, 1, GetDeploymentTestResult("actualSource", 6) },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), Enumerable.Range(2, 6).Select(v => GetFormattedDeploymentTestResult("actualSource", v, 2)), 10, 7, 6, 0, 7, GetDeploymentTestResult("actualSource", 7, 2) },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), new int[] { 1, 2, 3, 5, 6, 7 }.Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 6, 7, 0, null },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), new int[] { 1, 2, 3, 5, 7 }.Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 5, 7, 0, null },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), new int[] { 1, 7 }.Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 2, 7, 0, null },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), new int[] { 7 }.Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 1, 7, 0, null },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), new int[] { 1, 2, 3, 5, 6 }.Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 5, 6, 1, GetDeploymentTestResult("actualSource", 6) },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), new int[] { 1, 2, 3, 5 }.Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 4, 5, 2, GetDeploymentTestResult("actualSource", 5) },
                new object[] { Enumerable.Range(1, 7).Select(v => GetFormattedDeploymentTestResult("expectedSource", v)), new int[] { 2, 3, 5 }.Select(v => GetFormattedDeploymentTestResult("actualSource", v)), 10, 7, 3, 5, 2, GetDeploymentTestResult("actualSource", 5) },
            };
        static readonly string TestDescription = "dummy description";
        static readonly ushort UnmatchedResultsMaxSize = 10;

        [Fact]
        public void TestConstructorSuccess()
        {
            string expectedSource = "expectedSource";
            string actualSource = "actualSource";
            int batchSize = 10;

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, batchSize);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);

            var reportGenerator = new DeploymentTestReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                expectedSource,
                expectedResults.GetAsyncEnumerator(),
                actualSource,
                actualResults.GetAsyncEnumerator(),
                UnmatchedResultsMaxSize);

            Assert.Equal(TestDescription, reportGenerator.TestDescription);
            Assert.Equal(actualSource, reportGenerator.ActualSource);
            Assert.Equal(actualResults.GetAsyncEnumerator(), reportGenerator.ActualTestResults);
            Assert.Equal(expectedSource, reportGenerator.ExpectedSource);
            Assert.Equal(expectedResults.GetAsyncEnumerator(), reportGenerator.ExpectedTestResults);
            Assert.Equal(TestOperationResultType.Deployment.ToString(), reportGenerator.ResultType);
            Assert.Equal(typeof(DeploymentTestResultComparer), reportGenerator.TestResultComparer.GetType());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTestDescriptionIsNotProvided(string testDescription)
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DeploymentTestReportGenerator(
                    testDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
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
                () => new DeploymentTestReportGenerator(
                    TestDescription,
                    trackingId,
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
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
                () => new DeploymentTestReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    expectedSource,
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
                    UnmatchedResultsMaxSize));

            Assert.StartsWith("expectedSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenExpectedStoreIsNotProvided()
        {
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DeploymentTestReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    null,
                    "actualSource",
                    mockActualStore.Object,
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
                () => new DeploymentTestReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    actualSource,
                    mockActualStore.Object,
                    UnmatchedResultsMaxSize));

            Assert.StartsWith("actualSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenActualStoreIsNotProvided()
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DeploymentTestReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    null,
                    UnmatchedResultsMaxSize));

            Assert.Equal("actualTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(0)]
        public void TestConstructorThrowsWhenUnmatchedResultsMaxSizeIsNonPositive(ushort unmatchedResultsMaxSize)
        {
            var mockExpectedResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockActualStore = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new DeploymentTestReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    mockActualStore.Object,
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

            var reportGenerator = new DeploymentTestReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                expectedSource,
                expectedResults.GetAsyncEnumerator(),
                actualSource,
                actualResults.GetAsyncEnumerator(),
                UnmatchedResultsMaxSize);

            var report = (DeploymentTestReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(0UL, report.TotalExpectedDeployments);
            Assert.Equal(0UL, report.TotalActualDeployments);
            Assert.Equal(0UL, report.TotalMatchedDeployments);
            Assert.Equal(0, report.UnmatchedResults.Count);
        }

        [Theory]
        [MemberData(nameof(GetCreateReportData))]
        public async Task TestCreateReportAsync(
            IEnumerable<string> expectedStoreValues,
            IEnumerable<string> actualStoreValues,
            int batchSize,
            ulong totalExpectedDeployments,
            ulong totalActualDeployments,
            ulong totalMatchedDeployments,
            int expectedMissingResultsCount,
            DeploymentTestResult lastActualDeploymentTestResult)
        {
            string expectedSource = "expectedSource";
            string actualSource = "actualSource";
            string resultType = TestOperationResultType.Deployment.ToString();

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, batchSize);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);

            string trackingId = Guid.NewGuid().ToString();
            var reportGenerator = new DeploymentTestReportGenerator(
                TestDescription,
                trackingId,
                expectedSource,
                expectedResults.GetAsyncEnumerator(),
                actualSource,
                actualResults.GetAsyncEnumerator(),
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

            var report = (DeploymentTestReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(totalExpectedDeployments, report.TotalExpectedDeployments);
            Assert.Equal(totalActualDeployments, report.TotalActualDeployments);
            Assert.Equal(totalMatchedDeployments, report.TotalMatchedDeployments);
            Assert.Equal(expectedMissingResultsCount, report.UnmatchedResults.Count);

            if (lastActualDeploymentTestResult != null)
            {
                Assert.True(report.LastActualDeploymentTestResult.HasValue);

                var comparer = new DeploymentTestResultComparer();
                Assert.True(comparer.Matches(lastActualDeploymentTestResult.ToTestOperationResult(), report.LastActualDeploymentTestResult.OrDefault()));
            }
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

        private static string GetFormattedDeploymentTestResult(string source, int envVarEnd, int envVarStart = 1)
        {
            return GetDeploymentTestResult(source, envVarEnd, envVarStart).GetFormattedResult();
        }

        private static DeploymentTestResult GetDeploymentTestResult(string source, int envVarEnd, int envVarStart = 1)
        {
            var envVars = new Dictionary<string, string>();
            for (int i = envVarStart; i <= envVarEnd; i++)
            {
                envVars.Add($"Env_Key{i}", $"Env_Value{i}");
            }

            return new DeploymentTestResult("fb0f0a8b-d420-4dd1-8fbb-639e2e5d3863", source, envVars, DateTime.UtcNow);
        }
    }
}
