// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class DirectMethodReportGeneratorTest
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            // See TestCreateReportAsync for parameters names
            new List<object[]>
            {
                new object[]
                {
                    // NetworkOnSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new int[] { 200, 200, 200, 200, 200, 200, 200 },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 21, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 7, 0, 0, 0, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOffSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "4", "5", "6", "7" },
                    new int[] { 200, 200, 500, 200, 200, 200, 200 },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 16, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 1, 0, 0, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOnToleratedSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "4", "5", "6", "7" },
                    new int[] { 200, 200, 500, 200, 200, 200, 200 },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 20, 11),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 1, 0, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOffToleratedSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "4", "5", "6", "7" },
                    new int[] { 200, 200, 200, 200, 200, 200, 200 },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 15, 11),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 1, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOnFailure test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "5", "6", "7" },
                    new int[] { 200, 200, 200, 500, 200, 200, 200 },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 21, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 0, 1, 0, 0, 0, false
                },
                new object[]
                {
                    // NetworkOffFailure test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "4", "5", "6", "7" },
                    new int[] { 200, 200, 200, 200, 200, 200, 200 },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 16, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 0, 0, 1, 0, 0, false
                },
                new object[]
                {
                    // MismatchSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "5", "6", "7" },
                    new int[] { 200, 200, 200, 200, 200, 200, 200 },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 21, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 0, 0, 0, 1, 0, true
                },
                new object[]
                {
                    // MismatchFailure test
                    Enumerable.Range(1, 6).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "4", "5", "6", "7" },
                    new int[] { 200, 200, 200, 200, 200, 200, 200 },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 21, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 0, 0, 0, 0, 1, false
                },
                new object[]
                {
                    Enumerable.Range(1, 10).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "8", "10", "11" },
                    new int[] { 200, 200, 200, 500, 500, 500, 500, 200, 200, 200 },
                    new DateTime[]
                    {
                        // Smoke test for mixed results
                        new DateTime(2020, 1, 1, 9, 10, 12, 10), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 13, 10), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 15, 11), // NetworkOffToleratedSuccess
                        new DateTime(2020, 1, 1, 9, 10, 17, 10), // NetworkOffSuccess
                        new DateTime(2020, 1, 1, 9, 10, 18, 10), // NetworkOffSuccess
                        new DateTime(2020, 1, 1, 9, 10, 20, 12), // NetworkOnToleratedSuccess
                        new DateTime(2020, 1, 1, 9, 10, 21, 15), // NetworkOnFailure
                        new DateTime(2020, 1, 1, 9, 10, 24, 15),
                        new DateTime(2020, 1, 1, 9, 10, 24, 17),
                        new DateTime(2020, 1, 1, 9, 10, 25, 20) // NetworkOffFailure
                        // Mismatch Success is the missing 9
                        // MismatchFailure is the presence of 11 in the actualStoreValues
                    },
                    10, 3, 2, 1, 1, 1, 1, 1, 1, false
                },
                new object[]
                {
                    Enumerable.Range(1, 10).Select(v => v.ToString()),
                    new string[] { },
                    new int[] { 200, 200, 200, 500, 500, 500, 500, 200, 200, 200 },
                    new DateTime[]
                    {
                        // Smoke test for mixed results for edgeAgent scenario (aka when there are no receiverResults)
                        new DateTime(2020, 1, 1, 9, 10, 12, 10), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 13, 10), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 15, 11), // NetworkOffToleratedSuccess
                        new DateTime(2020, 1, 1, 9, 10, 17, 10), // NetworkOffSuccess
                        new DateTime(2020, 1, 1, 9, 10, 18, 10), // NetworkOffSuccess
                        new DateTime(2020, 1, 1, 9, 10, 20, 12), // NetworkOnToleratedSuccess
                        new DateTime(2020, 1, 1, 9, 10, 21, 15), // NetworkOnFailure
                        new DateTime(2020, 1, 1, 9, 10, 24, 15), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 24, 17), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 25, 20) // NetworkOffFailure
                    },
                    10, 4, 2, 1, 1, 1, 1, 0, 0, false
                },
            };

        static NetworkStatusTimeline NetworkStatusTimeline => MockNetworkStatusTimeline.GetMockAsync(new TimeSpan(0, 0, 0, 0, 5)).Result;

        [Fact]
        public void TestConstructorSuccess()
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            int batchSize = 10;
            string resultType = "resultType1";

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            var senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                receiverSource,
                receiverResults,
                resultType,
                new DirectMethodTestOperationResultComparer(),
                NetworkStatusTimeline);

            Assert.Equal(receiverSource, reportGenerator.ReceiverSource);
            Assert.Equal(senderResults, reportGenerator.SenderTestResults);
            Assert.Equal(senderSource, reportGenerator.SenderSource);
            Assert.Equal(receiverResults, reportGenerator.ReceiverTestResults);
            Assert.Equal(resultType, reportGenerator.ResultType);
            Assert.Equal(typeof(DirectMethodTestOperationResultComparer), reportGenerator.TestResultComparer.GetType());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    trackingId,
                    "senderSource",
                    mockSenderResults.Object,
                    "receiverSource",
                    receiverResults,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    NetworkStatusTimeline));

            Assert.StartsWith("trackingId", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenSenderSourceIsNotProvided(string senderSource)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    senderSource,
                    mockSenderResults.Object,
                    "receiverSource",
                    receiverResults,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    NetworkStatusTimeline));

            Assert.StartsWith("senderSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenSenderStoreIsNotProvided()
        {
            int batchSize = 10;
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    null,
                    "receiverSource",
                    receiverResults,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    NetworkStatusTimeline));

            Assert.Equal("senderTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenActualSourceIsNotProvided(string receiverSource)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    receiverSource,
                    receiverResults,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    NetworkStatusTimeline));

            Assert.StartsWith("receiverSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenActualStoreIsNotProvided()
        {
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    "receiverSource",
                    null,
                    "resultType1",
                    new DirectMethodTestOperationResultComparer(),
                    NetworkStatusTimeline));

            Assert.Equal("ReceiverTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    "receiverSource",
                    receiverResults,
                    resultType,
                    new DirectMethodTestOperationResultComparer(),
                    NetworkStatusTimeline));

            Assert.StartsWith("resultType", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenTestResultComparerIsNotProvided()
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    "receiverSource",
                    receiverResults,
                    "resultType1",
                    null,
                    NetworkStatusTimeline));

            Assert.Equal("testResultComparer", ex.ParamName);
        }

        [Fact]
        public async Task TestCreateReportAsyncWithEmptyResults()
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            int batchSize = 10;

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            var senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                receiverSource,
                receiverResults,
                "resultType1",
                new DirectMethodTestOperationResultComparer(),
                NetworkStatusTimeline);

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
            IEnumerable<string> senderStoreValues,
            IEnumerable<string> receiverStoreValues,
            IEnumerable<int> statusCodes,
            IEnumerable<DateTime> timestamps,
            int batchSize,
            ulong expectedNetworkOnSuccess,
            ulong expectedNetworkOffSuccess,
            ulong expectedNetworkOnToleratedSuccess,
            ulong expectedNetworkOffToleratedSuccess,
            ulong expectedNetworkOnFailure,
            ulong expectedNetworkOffFailure,
            ulong expectedMismatchSuccess,
            ulong expectedMismatchFailure,
            bool expectedIsPassed)
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            var senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                receiverSource,
                receiverResults,
                resultType,
                new DirectMethodTestOperationResultComparer(),
                NetworkStatusTimeline);

            var senderStoreData = GetSenderStoreData(senderSource, resultType, senderStoreValues, statusCodes, timestamps);
            for (int i = 0; i < senderStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockSenderStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(senderStoreData.Skip(startingOffset).Take(batchSize));
            }

            var receiverStoreData = GetReceiverStoreData(receiverSource, resultType, receiverStoreValues, timestamps);
            for (int j = 0; j < senderStoreData.Count; j += batchSize)
            {
                int startingOffset = j;
                mockReceiverStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(receiverStoreData.Skip(startingOffset).Take(batchSize));
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
            Assert.Equal(expectedIsPassed, report.IsPassed);
        }

        static List<(long, TestOperationResult)> GetSenderStoreData(
            string source,
            string resultType,
            IEnumerable<string> resultValues,
            IEnumerable<int> statusCodes,
            IEnumerable<DateTime> timestamps,
            int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            for (int i = 0; i < resultValues.Count(); i++)
            {
                DirectMethodTestResult directMethodTestResult = new DirectMethodTestResult(
                    source,
                    timestamps.ElementAt(i),
                    "1",
                    Guid.NewGuid(),
                    resultValues.ElementAt(i),
                    statusCodes.ElementAt(i).ToString());
                storeData.Add((count, new TestOperationResult(source, resultType, JsonConvert.SerializeObject(directMethodTestResult, Formatting.Indented), timestamps.ElementAt(i))));
                count++;
            }

            return storeData;
        }

        static List<(long, TestOperationResult)> GetReceiverStoreData(
            string source,
            string resultType,
            IEnumerable<string> resultValues,
            IEnumerable<DateTime> timestamps,
            int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            for (int i = 0; i < resultValues.Count(); i++)
            {
                DirectMethodTestResult directMethodTestResult = new DirectMethodTestResult(
                    source,
                    timestamps.ElementAt(i),
                    "1",
                    Guid.NewGuid(),
                    resultValues.ElementAt(i),
                    "fakeResult");
                storeData.Add((count, new TestOperationResult(source, resultType, JsonConvert.SerializeObject(directMethodTestResult, Formatting.Indented), timestamps.ElementAt(i))));
                count++;
            }

            return storeData;
        }
    }
}
