// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod;
    using global::TestResultCoordinator.Reports.DirectMethod.Connectivity;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class DirectMethodConnectivityReportGeneratorTest
    {
        static NetworkStatusTimeline NetworkStatusTimeline => MockNetworkStatusTimeline.GetMockAsync(new TimeSpan(0, 0, 0, 0, 5)).Result;
        static readonly string TestDescription = "dummy description";

        [Fact]
        public void TestConstructorSuccess()
        {
            string senderSource = "senderSource";
            Option<string> receiverSource = Option.Some("receiverSource");
            int batchSize = 10;
            string resultType = "resultType1";
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            var reportGenerator = new DirectMethodConnectivityReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults.GetAsyncEnumerator(),
                receiverSource,
                receiverResultsEnumerator,
                resultType,
                NetworkStatusTimeline,
                networkControllerType);

            Assert.Equal(TestDescription, reportGenerator.TestDescription);
            Assert.Equal(receiverSource, reportGenerator.ReceiverSource);
            Assert.Equal(senderResults.GetAsyncEnumerator(), reportGenerator.SenderTestResults);
            Assert.Equal(senderSource, reportGenerator.SenderSource);
            Assert.Equal(receiverResultsEnumerator, reportGenerator.ReceiverTestResults);
            Assert.Equal(resultType, reportGenerator.ResultType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            int batchSize = 10;
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodConnectivityReportGenerator(
                    TestDescription,
                    trackingId,
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResultsEnumerator,
                    "resultType1",
                    NetworkStatusTimeline,
                    networkControllerType));

            Assert.StartsWith("trackingId", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTestDescriptionIsNotProvided(string testDescription)
        {
            int batchSize = 10;
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodConnectivityReportGenerator(
                    testDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResultsEnumerator,
                    "resultType1",
                    NetworkStatusTimeline,
                    networkControllerType));

            Assert.StartsWith("testDescription", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenSenderSourceIsNotProvided(string senderSource)
        {
            int batchSize = 10;
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodConnectivityReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    senderSource,
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResultsEnumerator,
                    "resultType1",
                    NetworkStatusTimeline,
                    networkControllerType));

            Assert.StartsWith("senderSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenSenderStoreIsNotProvided()
        {
            int batchSize = 10;
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodConnectivityReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    null,
                    Option.Some("receiverSource"),
                    receiverResultsEnumerator,
                    "resultType1",
                    NetworkStatusTimeline,
                    networkControllerType));

            Assert.Equal("senderTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            int batchSize = 10;
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodConnectivityReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResultsEnumerator,
                    resultType,
                    NetworkStatusTimeline,
                    networkControllerType));

            Assert.StartsWith("resultType", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenReceiverSourceButNoReceiverTestResults()
        {
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodConnectivityReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    Option.None<IAsyncEnumerator<TestOperationResult>>(),
                    "resultType1",
                    NetworkStatusTimeline,
                    networkControllerType));

            Assert.Equal("Provide both receiverSource and receiverTestResults or neither.", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenReceiverTestResultsButNoReceiverSource()
        {
            int batchSize = 10;
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodConnectivityReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.None<string>(),
                    receiverResultsEnumerator,
                    "resultType1",
                    NetworkStatusTimeline,
                    networkControllerType));

            Assert.Equal("Provide both receiverSource and receiverTestResults or neither.", ex.Message);
        }

        [Fact]
        public async Task TestCreateReportAsyncWithEmptyResults()
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            int batchSize = 10;
            NetworkControllerType networkControllerType = NetworkControllerType.Offline;

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            var reportGenerator = new DirectMethodConnectivityReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults.GetAsyncEnumerator(),
                Option.Some(receiverSource),
                receiverResultsEnumerator,
                "resultType1",
                NetworkStatusTimeline,
                networkControllerType);

            var report = (DirectMethodConnectivityReport)await reportGenerator.CreateReportAsync();

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
        [MemberData(nameof(DirectMethodConnectivityReportDataWithSenderAndReceiverSource.GetCreateReportData), MemberType = typeof(DirectMethodConnectivityReportDataWithSenderAndReceiverSource))]
        public async Task TestCreateReportAsync(
            IEnumerable<ulong> senderStoreValues,
            IEnumerable<ulong> receiverStoreValues,
            IEnumerable<HttpStatusCode> statusCodes,
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
            bool expectedIsPassed,
            NetworkControllerType networkControllerType = NetworkControllerType.Offline)
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);
            Option<IAsyncEnumerator<TestOperationResult>> receiverResultsEnumerator = Option.Some<IAsyncEnumerator<TestOperationResult>>(receiverResults.GetAsyncEnumerator());

            var reportGenerator = new DirectMethodConnectivityReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults.GetAsyncEnumerator(),
                Option.Some(receiverSource),
                receiverResultsEnumerator,
                resultType,
                NetworkStatusTimeline,
                networkControllerType);

            Guid guid = Guid.NewGuid();
            var senderStoreData = GetSenderStoreData(senderSource, resultType, senderStoreValues, statusCodes, timestamps, guid);
            for (int i = 0; i < senderStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockSenderStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(senderStoreData.Skip(startingOffset).Take(batchSize));
            }

            var receiverStoreData = GetReceiverStoreData(receiverSource, resultType, receiverStoreValues, timestamps, guid);
            for (int j = 0; j < senderStoreData.Count; j += batchSize)
            {
                int startingOffset = j;
                mockReceiverStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(receiverStoreData.Skip(startingOffset).Take(batchSize));
            }

            var report = (DirectMethodConnectivityReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(expectedNetworkOffSuccess, report.NetworkOffSuccess);
            Assert.Equal(expectedNetworkOnToleratedSuccess, report.NetworkOnToleratedSuccess);
            Assert.Equal(expectedNetworkOffToleratedSuccess, report.NetworkOffToleratedSuccess);
            Assert.Equal(expectedNetworkOnFailure, report.NetworkOnFailure);
            Assert.Equal(expectedNetworkOffFailure, report.NetworkOffFailure);
            Assert.Equal(expectedMismatchSuccess, report.MismatchSuccess);
            Assert.Equal(expectedMismatchFailure, report.MismatchFailure);
            Assert.Equal(expectedIsPassed, report.IsPassed);
            Assert.Equal(expectedNetworkOnSuccess, report.NetworkOnSuccess);
        }

        [Theory]
        [MemberData(nameof(DirectMethodConnectivityReportDataWithSenderSourceOnly.GetCreateReportData), MemberType = typeof(DirectMethodConnectivityReportDataWithSenderSourceOnly))]
        public async Task TestCreateReportWithSenderResultsOnlyAsync(
            IEnumerable<ulong> senderStoreValues,
            IEnumerable<HttpStatusCode> statusCodes,
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
            bool expectedIsPassed,
            NetworkControllerType networkControllerType = NetworkControllerType.Offline)
        {
            string senderSource = "senderSource";
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResultsEnumerator = Option.None<IAsyncEnumerator<TestOperationResult>>();

            var reportGenerator = new DirectMethodConnectivityReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults.GetAsyncEnumerator(),
                Option.None<string>(),
                receiverResultsEnumerator,
                resultType,
                NetworkStatusTimeline,
                networkControllerType);

            var senderStoreData = GetSenderStoreData(senderSource, resultType, senderStoreValues, statusCodes, timestamps, Guid.NewGuid());
            for (int i = 0; i < senderStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockSenderStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(senderStoreData.Skip(startingOffset).Take(batchSize));
            }

            var report = (DirectMethodConnectivityReport)await reportGenerator.CreateReportAsync();

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
            IEnumerable<ulong> resultValues,
            IEnumerable<HttpStatusCode> statusCodes,
            IEnumerable<DateTime> timestamps,
            Guid guid,
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
                    guid,
                    resultValues.ElementAt(i),
                    statusCodes.ElementAt(i));
                storeData.Add((count, new TestOperationResult(source, resultType, JsonConvert.SerializeObject(directMethodTestResult, Formatting.Indented), timestamps.ElementAt(i))));
                count++;
            }

            return storeData;
        }

        static List<(long, TestOperationResult)> GetReceiverStoreData(
            string source,
            string resultType,
            IEnumerable<ulong> resultValues,
            IEnumerable<DateTime> timestamps,
            Guid guid,
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
                    guid,
                    resultValues.ElementAt(i),
                    HttpStatusCode.OK);
                storeData.Add((count, new TestOperationResult(source, resultType, JsonConvert.SerializeObject(directMethodTestResult, Formatting.Indented), timestamps.ElementAt(i))));
                count++;
            }

            return storeData;
        }
    }
}
