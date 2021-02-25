// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod.LongHaul;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class DirectMethodLongHaulReportGeneratorTest
    {
        static readonly string TestDescription = "dummy description";

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

            var reportGenerator = new DirectMethodLongHaulReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                receiverSource,
                receiverResults,
                resultType);

            Assert.Equal(TestDescription, reportGenerator.TestDescription);
            Assert.Equal(receiverSource, reportGenerator.ReceiverSource);
            Assert.Equal(senderResults, reportGenerator.SenderTestResults);
            Assert.Equal(senderSource, reportGenerator.SenderSource);
            Assert.Equal(receiverResults, reportGenerator.ReceiverTestResults);
            Assert.Equal(resultType, reportGenerator.ResultType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTestDescriptionIsNotProvided(string testDescription)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
               new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodLongHaulReportGenerator(
                    testDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResults,
                    "resultType1"));

            Assert.StartsWith("testDescription", ex.Message);
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
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    trackingId,
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResults,
                    "resultType1"));

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
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    senderSource,
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResults,
                    "resultType1"));

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
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    null,
                    Option.Some("receiverSource"),
                    receiverResults,
                    "resultType1"));

            Assert.Equal("senderTestResults", ex.ParamName);
        }

        [Fact]
        public void TestConstructorThrowsWhenReceiverResultsButNoReceiverSource()
        {
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.None<string>(),
                    Option.Some(mockReceiverResults.Object),
                    "resultType1"));

            Assert.Equal("Provide both receiverSource and receiverTestResults or neither.", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenReceiverSourceButNoReceiverResults()
        {
            var mockSenderResults = new Mock<ITestResultCollection<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    Option.None<ITestResultCollection<TestOperationResult>>(),
                    "resultType1"));

            Assert.Equal("Provide both receiverSource and receiverTestResults or neither.", ex.Message);
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
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    mockSenderResults.Object,
                    Option.Some("receiverSource"),
                    receiverResults,
                    resultType));

            Assert.StartsWith("resultType", ex.Message);
        }

        [Theory]
        [MemberData(nameof(DirectMethodLongHaulReportData.GetCreateReportData), MemberType = typeof(DirectMethodLongHaulReportData))]
        public async Task TestCreateReportAsync(
            IEnumerable<ulong> senderStoreValues,
            IEnumerable<ulong> receiverStoreValues,
            IEnumerable<HttpStatusCode> statusCodes,
            IEnumerable<DateTime> timestamps,
            int batchSize,
            bool expectedIsPassed,
            long expectedOk,
            long expectedStatusCodeZero,
            long expectedUnknown)
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            var senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            var receiverResults = Option.Some<ITestResultCollection<TestOperationResult>>(
                new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize));

            var reportGenerator = new DirectMethodLongHaulReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                Option.Some(receiverSource),
                receiverResults,
                resultType);

            Guid guid = Guid.NewGuid();

            List<(long, TestOperationResult)> senderStoreData = GetSenderStoreData(senderSource, resultType, senderStoreValues, statusCodes, timestamps, guid);
            for (int i = 0; i < senderStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockSenderStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(senderStoreData.Skip(startingOffset).Take(batchSize));
            }

            List<(long, TestOperationResult)> receiverStoreData = GetReceiverStoreData(receiverSource, resultType, receiverStoreValues, timestamps, guid);
            for (int j = 0; j < receiverStoreData.Count; j += batchSize)
            {
                int startingOffset = j;
                mockReceiverStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(receiverStoreData.Skip(startingOffset).Take(batchSize));
            }

            var report = (DirectMethodLongHaulReport)await reportGenerator.CreateReportAsync();
            Assert.Equal(expectedIsPassed, report.IsPassed);
            Assert.Equal(expectedOk, report.SenderSuccesses);
            Assert.Equal(expectedStatusCodeZero, report.StatusCodeZero);
            Assert.Equal(expectedUnknown, report.Other.Sum(x => x.Value));
        }

        [Theory]
        [MemberData(nameof(DirectMethodLongHaulReportData.GetCreateReportData), MemberType = typeof(DirectMethodLongHaulReportData))]
        public async Task TestCreateReportWithSenderResultsOnlyAsync(
            IEnumerable<ulong> senderStoreValues,
            IEnumerable<ulong> receiverStoreValues,
            IEnumerable<HttpStatusCode> statusCodes,
            IEnumerable<DateTime> timestamps,
            int batchSize,
            bool expectedIsPassed,
            long expectedOk,
            long expectedStatusCodeZero,
            long expectedUnknown)
        {
            string senderSource = "senderSource";
            var values = receiverStoreValues;
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            var senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var receiverResults = Option.None<ITestResultCollection<TestOperationResult>>();

            var reportGenerator = new DirectMethodLongHaulReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                Option.None<string>(),
                receiverResults,
                resultType);

            var senderStoreData = GetSenderStoreData(senderSource, resultType, senderStoreValues, statusCodes, timestamps, Guid.NewGuid());
            for (int i = 0; i < senderStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockSenderStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(senderStoreData.Skip(startingOffset).Take(batchSize));
            }

            var report = (DirectMethodLongHaulReport)await reportGenerator.CreateReportAsync();
            Assert.Equal(expectedIsPassed, report.IsPassed);
            Assert.Equal(expectedOk, report.SenderSuccesses);
            Assert.Equal(expectedStatusCodeZero, report.StatusCodeZero);
            Assert.Equal(expectedUnknown, report.Other.Sum(x => x.Value));
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

            var reportGenerator = new DirectMethodLongHaulReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                senderResults,
                Option.Some(receiverSource),
                receiverResults,
                "resultType1");

            var report = (DirectMethodLongHaulReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(0L, report.ReceiverSuccesses.Expect<ArgumentException>(() => throw new ArgumentException("impossible")));
            Assert.Equal(0L, report.SenderSuccesses);
            Assert.Equal(0L, report.StatusCodeZero);
            Assert.Equal(0L, report.Other.Sum(x => x.Value));
            Assert.True(report.IsPassed);
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
