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
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class DirectMethodReportGeneratorTest
    {
        static NetworkStatusTimeline NetworkStatusTimeline => MockNetworkStatusTimeline.GetMockAsync(new TimeSpan(0, 0, 0, 0, 5)).Result;

        [Fact]
        public void TestConstructorSuccess()
        {
            string senderSource = "senderSource";
            Option<string> receiverSource = Option.Some("receiverSource");
            int batchSize = 10;
            string resultType = "resultType1";

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            var senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
                new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                receiverSource,
                receiverResults,
                resultType,
                NetworkStatusTimeline);

            Assert.Equal(receiverSource, reportGenerator.ReceiverSource);
            Assert.Equal(senderResults, reportGenerator.SenderTestResults);
            Assert.Equal(senderSource, reportGenerator.SenderSource);
            Assert.Equal(receiverResults, reportGenerator.ReceiverTestResults);
            Assert.Equal(resultType, reportGenerator.ResultType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
               new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    trackingId,
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResults,
                    "resultType1",
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
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
               new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    senderSource,
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResults,
                    "resultType1",
                    NetworkStatusTimeline));

            Assert.StartsWith("senderSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenSenderStoreIsNotProvided()
        {
            int batchSize = 10;
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
               new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    null,
                    Option.Some("receiverSource"),
                    receiverResults,
                    "resultType1",
                    NetworkStatusTimeline));

            Assert.Equal("senderTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
                new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResults,
                    resultType,
                    NetworkStatusTimeline));

            Assert.StartsWith("resultType", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenReceiverSourceButNoReceiverTestResults()
        {
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    Option.None<ITestResultCollection<TestOperationResult>>(),
                    "resultType1",
                    NetworkStatusTimeline));

            Assert.Equal("Provide both receiverSource and receiverTestResults or neither.", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenReceiverTestResultsButNoReceiverSource()
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
                new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodReportGenerator(
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.None<string>(),
                    receiverResults,
                    "resultType1",
                    NetworkStatusTimeline));

            Assert.Equal("Provide both receiverSource and receiverTestResults or neither.", ex.Message);
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
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
                new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                Option.Some(receiverSource),
                receiverResults,
                "resultType1",
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
        [MemberData(nameof(DirectMethodReportDataWithSenderAndReceiverSource.GetCreateReportData), MemberType = typeof(DirectMethodReportDataWithSenderAndReceiverSource))]
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
            bool expectedIsPassed)
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            var senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
                new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                Option.Some(receiverSource),
                receiverResults,
                resultType,
                NetworkStatusTimeline);

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

        [Theory]
        [MemberData(nameof(DirectMethodReportDataWithSenderSourceOnly.GetCreateReportData), MemberType = typeof(DirectMethodReportDataWithSenderSourceOnly))]
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
            bool expectedIsPassed)
        {
            string senderSource = "senderSource";
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            var senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.None<ITestResultCollection<TestOperationResult>>();

            var reportGenerator = new DirectMethodReportGenerator(
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                Option.None<string>(),
                receiverResults,
                resultType,
                NetworkStatusTimeline);

            var senderStoreData = GetSenderStoreData(senderSource, resultType, senderStoreValues, statusCodes, timestamps, Guid.NewGuid());
            for (int i = 0; i < senderStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockSenderStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(senderStoreData.Skip(startingOffset).Take(batchSize));
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
