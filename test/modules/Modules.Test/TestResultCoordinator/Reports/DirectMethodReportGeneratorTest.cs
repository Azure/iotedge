// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::TestResultCoordinator.Report.DirectMethodReport;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethodReport;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Moq;
    using Xunit;

    public class DirectMethodReportGeneratorTest
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
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "2", "2", "2", "3", "4", "4", "5", "6" }, 10, 7, 6, 3, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "1", "2", "3", "4", "5", "6", "6" }, 10, 7, 6, 2, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "2", "3", "4", "5", "6" }, 4, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "6", "7" }, 4, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6", "7" }, 4, 7, 6, 0, 1 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "6", "7" }, 4, 7, 5, 0, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "1", "3", "4", "5", "6" }, 4, 7, 5, 0, 2 },
                new object[] { Enumerable.Range(1, 7).Select(v => v.ToString()), new[] { "2", "3", "4", "5", "7" }, 4, 7, 5, 0, 2 },
            };

        public static DateTime[] DateTimeArray => new[]
       {
            new DateTime(2020, 1, 1, 9, 10, 10, 10),
            new DateTime(2020, 1, 1, 9, 10, 10, 13),
            new DateTime(2020, 1, 1, 9, 10, 15, 10),
            new DateTime(2020, 1, 1, 9, 10, 15, 13),
            new DateTime(2020, 1, 1, 9, 10, 20, 10),
            new DateTime(2020, 1, 1, 9, 10, 20, 13),
            new DateTime(2020, 1, 1, 9, 10, 25, 10),
            new DateTime(2020, 1, 1, 9, 10, 25, 13)
        };

        public static string[] NetworkControllerStatusArray => new[] { "Enabled", "Enabled", "Disabled", "Disabled", "Enabled", "Enabled", "Disabled", "Disabled" };

        public static string[] NetworkControllerOperationArray => new[] { "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet" };

        [Fact]
        public async void TestConstructorSuccess()
        {
            string expectedSource = "expectedSource";
            string actualSource = "actualSource";
            int batchSize = 10;
            string resultType = "resultType1";

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, batchSize);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(actualResults, new TimeSpan(0, 0, 0, 0, 5));

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                expectedSource,
                expectedResults,
                actualSource,
                actualResults,
                resultType,
                new DirectMethodTestOperationResultComparer(),
                networkStatusTimeline);

            Assert.Equal(actualSource, reportGenerator.ActualSource);
            Assert.Equal(actualResults, reportGenerator.ActualTestResults);
            Assert.Equal(expectedSource, reportGenerator.ExpectedSource);
            Assert.Equal(expectedResults, reportGenerator.ExpectedTestResults);
            Assert.Equal(resultType, reportGenerator.ResultType);
            Assert.Equal(typeof(DirectMethodTestOperationResultComparer), reportGenerator.TestResultComparer.GetType());
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            int batchSize = 10;
            var mockExpectedResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(actualResults, new TimeSpan(0, 0, 0, 0, 5));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    trackingId,
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    actualResults,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    networkStatusTimeline));

            Assert.StartsWith("trackingId", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async void TestConstructorThrowsWhenExpectedSourceIsNotProvided(string expectedSource)
        {
            int batchSize = 10;
            var mockExpectedResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(actualResults, new TimeSpan(0, 0, 0, 0, 5));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    expectedSource,
                    mockExpectedResults.Object,
                    "actualSource",
                    actualResults,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    networkStatusTimeline));

            Assert.StartsWith("expectedSource", ex.Message);
        }

        [Fact]
        public async void TestConstructorThrowsWhenExpectedStoreIsNotProvided()
        {
            int batchSize = 10;
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(actualResults, new TimeSpan(0, 0, 0, 0, 5));

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    null,
                    "actualSource",
                    actualResults,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    networkStatusTimeline));

            Assert.Equal("expectedTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async void TestConstructorThrowsWhenActualSourceIsNotProvided(string actualSource)
        {
            int batchSize = 10;
            var mockExpectedResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(actualResults, new TimeSpan(0, 0, 0, 0, 5));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    actualSource,
                    actualResults,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    networkStatusTimeline));

            Assert.StartsWith("actualSource", ex.Message);
        }

        [Fact]
        public async void TestConstructorThrowsWhenActualStoreIsNotProvided()
        {
            int batchSize = 10;
            var mockExpectedResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(actualResults, new TimeSpan(0, 0, 0, 0, 5));

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    null,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    networkStatusTimeline));

            Assert.Equal("actualTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            int batchSize = 10;
            var mockExpectedResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(actualResults, new TimeSpan(0, 0, 0, 0, 5));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    actualResults,
                    resultType,
                    new DirectMethodTestOperationResultComparer(),
                    networkStatusTimeline));

            Assert.StartsWith("resultType", ex.Message);
        }

        [Fact]
        public async void TestConstructorThrowsWhenTestResultComparerIsNotProvided()
        {
            int batchSize = 10;
            var mockExpectedResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(actualResults, new TimeSpan(0, 0, 0, 0, 5));

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "expectedSource",
                    mockExpectedResults.Object,
                    "actualSource",
                    actualResults,
                    "resultType1",
                    null,
                    networkStatusTimeline));

            Assert.Equal("testResultComparer", ex.ParamName);
        }

        [Fact]
        public async Task TestCreateReportAsyncWithEmptyResults()
        {
            string expectedSource = "expectedSource";
            string actualSource = "actualSource";
            int batchSize = 10;
            TimeSpan tolerancePeriod = new TimeSpan(0, 0, 0, 0, 5);

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, batchSize);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(
                GetStoreTestResultCollection(
                    NetworkControllerStatusArray,
                    DateTimeArray,
                    NetworkControllerOperationArray),
                tolerancePeriod);

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                expectedSource,
                expectedResults,
                actualSource,
                actualResults,
                "resultType1",
                new DirectMethodTestOperationResultComparer(),
                networkStatusTimeline);

            var report = (DirectMethodReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(0UL, report.NetworkOnSuccess);
            Assert.Equal(0UL, report.NetworkOffSuccess);
            Assert.Equal(0UL, report.NetworkOnToleratedSuccess);
            Assert.Equal(0UL, report.NetworkOffToleratedSuccess);
            Assert.Equal(0UL, report.NetworkOnFailure);
            Assert.Equal(0UL, report.NetworkOffFailure);
            Assert.Equal(0UL, report.MismatchSuccess);
            Assert.Equal(0UL, report.MismatchFailure);
        }

        [Theory]
        [MemberData(nameof(GetCreateReportData))]
        public async Task TestCreateReportAsync(
            IEnumerable<string> expectedStoreValues,
            IEnumerable<string> actualStoreValues,
            int batchSize,
            ulong expectedNetworkOnSuccess,
            ulong expectedNetworkOffSuccess,
            ulong expectedNetworkOnToleratedSuccess,
            ulong expectedNetworkOffToleratedSuccess,
            ulong expectedNetworkOnFailure,
            ulong expectedNetworkOffFailure,
            ulong expectedMismatchSuccess,
            ulong expectedMismatchFailure)
        {
            string expectedSource = "expectedSource";
            string actualSource = "actualSource";
            string resultType = TestOperationResultType.Messages.ToString();
            TimeSpan tolerancePeriod = new TimeSpan(0, 0, 0, 0, 5);

            var mockExpectedStore = new Mock<ISequentialStore<TestOperationResult>>();
            var expectedResults = new StoreTestResultCollection<TestOperationResult>(mockExpectedStore.Object, batchSize);
            var mockActualStore = new Mock<ISequentialStore<TestOperationResult>>();
            var actualResults = new StoreTestResultCollection<TestOperationResult>(mockActualStore.Object, batchSize);
            var networkStatusTimeline = await GetNetworkStatusTimeline(
                GetStoreTestResultCollection(
                    NetworkControllerStatusArray,
                    DateTimeArray,
                    NetworkControllerOperationArray),
                tolerancePeriod);

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                expectedSource,
                expectedResults,
                actualSource,
                actualResults,
                resultType,
                new DirectMethodTestOperationResultComparer(),
                networkStatusTimeline);

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

            var report = (DirectMethodReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(expectedNetworkOnSuccess, report.NetworkOnSuccess);
            Assert.Equal(expectedNetworkOffSuccess, report.NetworkOffSuccess);
            Assert.Equal(expectedNetworkOnToleratedSuccess, report.NetworkOnToleratedSuccess);
            Assert.Equal(expectedNetworkOffToleratedSuccess, report.NetworkOffToleratedSuccess);
            Assert.Equal(expectedNetworkOnFailure, report.NetworkOnFailure);
            Assert.Equal(expectedNetworkOffFailure, report.NetworkOffFailure);
            Assert.Equal(expectedMismatchSuccess, report.MismatchSuccess);
            Assert.Equal(expectedMismatchFailure, report.MismatchFailure);
        }

        private StoreTestResultCollection<TestOperationResult> GetStoreTestResultCollection(
            IEnumerable<string> resultValues,
            IEnumerable<DateTime> resultDates,
            IEnumerable<string> resultOperations)
        {
            int batchSize = 500;
            string source = "testSource";
            var expectedStoreData = GetNetworkControllerStoreData(source, resultValues, resultDates, resultOperations);
            var mockResultStore = new Mock<ISequentialStore<TestOperationResult>>();
            var resultCollection = new StoreTestResultCollection<TestOperationResult>(mockResultStore.Object, batchSize);
            for (int i = 0; i < expectedStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockResultStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(expectedStoreData.Skip(startingOffset).Take(batchSize));
            }

            return resultCollection;
        }

        async Task<Option<NetworkStatusTimeline>> GetNetworkStatusTimeline(StoreTestResultCollection<TestOperationResult> results, TimeSpan tolerancePeriod)
        {
            try
            {
                return Option.Some(await NetworkStatusTimeline.Create(results, tolerancePeriod));
            }
            catch (Exception)
            {
                return Option.None<NetworkStatusTimeline>();
            }
        }

        static List<(long, TestOperationResult)> GetNetworkControllerStoreData(
            string source,
            IEnumerable<string> resultValues,
            IEnumerable<DateTime> resultDates,
            IEnumerable<string> resultOperations,
            int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            for (int i = 0; i < resultValues.Count(); i++)
            {
                var networkControllerStatus = (NetworkControllerStatus)Enum.Parse(typeof(NetworkControllerStatus), resultValues.ElementAt(i));
                var networkControllerOperation = (NetworkControllerOperation)Enum.Parse(typeof(NetworkControllerOperation), resultOperations.ElementAt(i));
                var networkControllerTestResult = new NetworkControllerTestResult(
                    source, resultDates.ElementAt(i))
                { NetworkControllerStatus = networkControllerStatus, NetworkControllerType = NetworkControllerType.Offline, Operation = networkControllerOperation };
                storeData.Add((count, networkControllerTestResult.ToTestOperationResult()));
                count++;
            }

            return storeData;
        }
        static List<(long, TestOperationResult)> GetStoreData(string source, string resultType, IEnumerable<string> resultValues, int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            for (int i = 0; i < resultValues.Count(); i++)
            {
                storeData.Add((count, new TestOperationResult(source, resultType, resultValues.ElementAt(i), DateTimeArray[i])));
            }
            foreach (string value in resultValues)
            {
                storeData.Add((count, new TestOperationResult(source, resultType, value, DateTime.UtcNow)));
                count++;
            }

            return storeData;
        }
    }
}
