// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using global::TestResultCoordinator;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod.LongHaul;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
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
            string receiverSource = "receiverSource";
            int batchSize = 10;
            string resultType = "resultType1";

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            var reportGenerator = new DirectMethodLongHaulReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                Topology.SingleNode,
                false,
                senderResults.GetAsyncEnumerator(),
                receiverSource,
                receiverResults.GetAsyncEnumerator(),
                resultType);

            Assert.Equal(TestDescription, reportGenerator.TestDescription);
            Assert.Equal(receiverSource, reportGenerator.ReceiverSource);
            Assert.Equal(senderResults.GetAsyncEnumerator(), reportGenerator.SenderTestResults);
            Assert.Equal(senderSource, reportGenerator.SenderSource);
            Assert.Equal(receiverResults.GetAsyncEnumerator(), reportGenerator.ReceiverTestResults);
            Assert.Equal(resultType, reportGenerator.ResultType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTestDescriptionIsNotProvided(string testDescription)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodLongHaulReportGenerator(
                    testDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    Topology.SingleNode,
                    false,
                    mockSenderResults.Object,
                    "receiverSource",
                    receiverResults.GetAsyncEnumerator(),
                    "resultType1"));

            Assert.StartsWith("testDescription", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenTrackingIdIsNotProvided(string trackingId)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    trackingId,
                    "senderSource",
                    Topology.SingleNode,
                    false,
                    mockSenderResults.Object,
                    "receiverSource",
                    receiverResults.GetAsyncEnumerator(),
                    "resultType1"));

            Assert.StartsWith("trackingId", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenSenderSourceIsNotProvided(string senderSource)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    senderSource,
                    Topology.SingleNode,
                    false,
                    mockSenderResults.Object,
                    "receiverSource",
                    receiverResults.GetAsyncEnumerator(),
                    "resultType1"));

            Assert.StartsWith("senderSource", ex.Message);
        }

        [Fact]
        public void TestConstructorThrowsWhenSenderStoreIsNotProvided()
        {
            int batchSize = 10;
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    Topology.SingleNode,
                    false,
                    null,
                    "receiverSource",
                    receiverResults.GetAsyncEnumerator(),
                    "resultType1"));

            Assert.Equal("senderTestResults", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TestConstructorThrowsWhenResultTypeIsNotProvided(string resultType)
        {
            int batchSize = 10;
            var mockSenderResults = new Mock<IAsyncEnumerator<TestOperationResult>>();
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => new DirectMethodLongHaulReportGenerator(
                    TestDescription,
                    Guid.NewGuid().ToString(),
                    "senderSource",
                    Topology.SingleNode,
                    false,
                    mockSenderResults.Object,
                    "receiverSource",
                    receiverResults.GetAsyncEnumerator(),
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
            long expectedUnauthorizedError,
            long expectedDeviceNotFound,
            long expectedTransientError,
            long expectedResourceError,
            long expectedOther)
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            var reportGenerator = new DirectMethodLongHaulReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                Topology.SingleNode,
                false,
                senderResults.GetAsyncEnumerator(),
                receiverSource,
                receiverResults.GetAsyncEnumerator(),
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
            Assert.Equal(expectedUnauthorizedError, report.Unauthorized);
            Assert.Equal(expectedDeviceNotFound, report.DeviceNotFound);
            Assert.Equal(expectedTransientError, report.TransientError);
            Assert.Equal(expectedResourceError, report.ResourceError);
            Assert.Equal(expectedOther, report.Other.Sum(x => x.Value));
        }

        [Fact]
        public async Task TestOtherStatusCodeCounts()
        {
            var x = DirectMethodLongHaulReportData.GetStatusCodeTestData;
            IEnumerable<ulong> senderStoreValues = (IEnumerable<ulong>)x[0];
            IEnumerable<ulong> receiverStoreValues = (IEnumerable<ulong>)x[1];
            IEnumerable<HttpStatusCode> statusCodes = (IEnumerable<HttpStatusCode>)x[2];
            IEnumerable<DateTime> timestamps = (IEnumerable<DateTime>)x[3];
            int batchSize = (int)x[4];
            bool expectedIsPassed = (bool)x[5];
            long expectedOk = (long)x[6];
            long expectedStatusCodeZero = (long)x[7];
            long expectedDeviceNotFound = (long)x[8];
            long expectedUnauthorized = (long)x[9];
            long expectedTransientError = (long)x[10];
            long expectedResourceError = (long)x[11];
            Dictionary<HttpStatusCode, long> expectedOtherDict = (Dictionary<HttpStatusCode, long>)x[12];

            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            string resultType = TestOperationResultType.DirectMethod.ToString();

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            var reportGenerator = new DirectMethodLongHaulReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                Topology.SingleNode,
                false,
                senderResults.GetAsyncEnumerator(),
                receiverSource,
                receiverResults.GetAsyncEnumerator(),
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
            Assert.Equal(expectedUnauthorized, report.Unauthorized);
            Assert.Equal(expectedDeviceNotFound, report.DeviceNotFound);
            Assert.Equal(expectedTransientError, report.TransientError);
            Assert.Equal(expectedResourceError, report.ResourceError);
            Assert.Equal(expectedOtherDict.Sum(x => x.Value), report.Other.Sum(x => x.Value));
            Assert.Equal(expectedOtherDict[HttpStatusCode.InternalServerError], report.Other[HttpStatusCode.InternalServerError]);
        }

        [Fact]
        public async Task TestCreateReportAsyncWithEmptyResults()
        {
            string senderSource = "senderSource";
            string receiverSource = "receiverSource";
            int batchSize = 10;

            var mockSenderStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> senderResults = new StoreTestResultCollection<TestOperationResult>(mockSenderStore.Object, batchSize);
            var mockReceiverStore = new Mock<ISequentialStore<TestOperationResult>>();
            IAsyncEnumerable<TestOperationResult> receiverResults = new StoreTestResultCollection<TestOperationResult>(mockReceiverStore.Object, batchSize);

            var reportGenerator = new DirectMethodLongHaulReportGenerator(
                TestDescription,
                Guid.NewGuid().ToString(),
                senderSource,
                Topology.SingleNode,
                false,
                senderResults.GetAsyncEnumerator(),
                receiverSource,
                receiverResults.GetAsyncEnumerator(),
                "resultType1");

            var report = (DirectMethodLongHaulReport)await reportGenerator.CreateReportAsync();

            Assert.Equal(0L, report.ReceiverSuccesses);
            Assert.Equal(0L, report.SenderSuccesses);
            Assert.Equal(0L, report.StatusCodeZero);
            Assert.Equal(0L, report.Unauthorized);
            Assert.Equal(0L, report.DeviceNotFound);
            Assert.Equal(0L, report.TransientError);
            Assert.Equal(0L, report.ResourceError);
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
