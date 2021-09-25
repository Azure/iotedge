// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod;
    using global::TestResultCoordinator.Reports.DirectMethod.Connectivity;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class NetworkStatusTimelineTest
    {
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

        public static string[] NetworkControllerStatusArray => new[] { "Disabled", "Disabled", "Enabled", "Enabled", "Disabled", "Disabled", "Enabled", "Enabled" };

        public static string[] NetworkControllerOperationArray => new[] { "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet" };

        public static IEnumerable<object[]> GetCreateReportData =>
            new List<object[]> { new object[] { NetworkControllerStatusArray, DateTimeArray, NetworkControllerOperationArray } };

        public static IEnumerable<object[]> GetInvalidCreateReportDataWithIdenticalConsecutiveOperations =>
            new List<object[]>
                {
                    new object[]
                    {
                        NetworkControllerStatusArray,
                        DateTimeArray,
                        new[] { "SettingRule", "SettingRule", "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet" }
                    }
                };

        public static IEnumerable<object[]> GetInvalidCreateReportDataWithNoEndRuleSet =>
            new List<object[]>
                {
                    new object[]
                    {
                        new[] { "Disabled", "Disabled", "Enabled", "Enabled", "Disabled", "Disabled", "Enabled" },
                        DateTimeArray,
                        new[] { "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule" }
                    }
                };
        public static IEnumerable<object[]> GetInvalidCreateReportDataWithFirstOperationIncorrect =>
           new List<object[]>
               {
                    new object[]
                    {
                        NetworkControllerStatusArray,
                        DateTimeArray,
                        new[] { "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule" }
                    }
               };
        public static IEnumerable<object[]> GetInvalidCreateReportDataWithMismatchedNetworkStatuses =>
            new List<object[]>
                {
                    new object[]
                    {
                        new[] { "Disabled", "Disabled", "Enabled", "Disabled", "Disabled", "Disabled", "Enabled", "Enabled" },
                        DateTimeArray,
                        NetworkControllerOperationArray
                    }
                };

        public static IEnumerable<object[]> GetInvalidCreateReportDataWithEmptyResults =>
            new List<object[]> { new object[] { new string[] { }, new DateTime[] { }, new string[] { } } };

        [Theory]
        [MemberData(nameof(GetCreateReportData))]
        public async void TestCreateNetworkStatusTimeline(
            IEnumerable<string> networkControllerResultValues,
            IEnumerable<DateTime> networkControllerResultDates,
            IEnumerable<string> networkControllerResultOperations)
        {
            IAsyncEnumerable<TestOperationResult> resultCollection = this.GetStoreTestResultCollection(networkControllerResultValues, networkControllerResultDates, networkControllerResultOperations);
            NetworkStatusTimeline timeline = await NetworkStatusTimeline.CreateAsync(resultCollection.GetAsyncEnumerator(), new TimeSpan(0, 0, 0, 0, 5));

            (NetworkControllerStatus status, bool inTolerance, TimeSpan timeDiff) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 11, 10));
            Assert.Equal(NetworkControllerStatus.Disabled, status);
            Assert.False(inTolerance);
            (status, inTolerance, _) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 16, 10));
            Assert.Equal(NetworkControllerStatus.Enabled, status);
            Assert.False(inTolerance);
            (status, inTolerance, _) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 20, 15));
            Assert.Equal(NetworkControllerStatus.Disabled, status);
            Assert.True(inTolerance);
            (status, inTolerance, _) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 25, 11));
            Assert.Equal(NetworkControllerStatus.Enabled, status);
            Assert.True(inTolerance);
            (status, inTolerance, _) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 25, 12));
            Assert.Equal(NetworkControllerStatus.Enabled, status);
            Assert.True(inTolerance);
        }

        [Theory]
        [MemberData(nameof(GetInvalidCreateReportDataWithIdenticalConsecutiveOperations))]
        public async void TestCreateNetworkStatusTimelineWithIdenticalConsecutiveOperations(
            IEnumerable<string> networkControllerResultValues,
            IEnumerable<DateTime> networkControllerResultDates,
            IEnumerable<string> networkControllerResultOperations)
        {
            IAsyncEnumerable<TestOperationResult> resultCollection = this.GetStoreTestResultCollection(networkControllerResultValues, networkControllerResultDates, networkControllerResultOperations);
            NetworkStatusTimeline timeline = await NetworkStatusTimeline.CreateAsync(resultCollection.GetAsyncEnumerator(), new TimeSpan(0, 0, 0, 0, 5));
            var ex = Assert.Throws<InvalidOperationException>(() => timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 16, 10)));
            Assert.Equal("Test result SettingRule found with no RuleSet found after.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetInvalidCreateReportDataWithNoEndRuleSet))]
        public async void TestCreateNetworkStatusTimelineWithNoEndRuleSet(
            IEnumerable<string> networkControllerResultValues,
            IEnumerable<DateTime> networkControllerResultDates,
            IEnumerable<string> networkControllerResultOperations)
        {
            IAsyncEnumerable<TestOperationResult> resultCollection = this.GetStoreTestResultCollection(networkControllerResultValues, networkControllerResultDates, networkControllerResultOperations);
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await NetworkStatusTimeline.CreateAsync(resultCollection.GetAsyncEnumerator(), new TimeSpan(0, 0, 0, 0, 5)));
            Assert.Equal("Network Controller Test Results must have an even number of results.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetInvalidCreateReportDataWithFirstOperationIncorrect))]
        public async void TestCreateNetworkStatusTimelineWithFirstOperationIncorrect(
            IEnumerable<string> networkControllerResultValues,
            IEnumerable<DateTime> networkControllerResultDates,
            IEnumerable<string> networkControllerResultOperations)
        {
            IAsyncEnumerable<TestOperationResult> resultCollection = this.GetStoreTestResultCollection(networkControllerResultValues, networkControllerResultDates, networkControllerResultOperations);
            NetworkStatusTimeline timeline = await NetworkStatusTimeline.CreateAsync(resultCollection.GetAsyncEnumerator(), new TimeSpan(0, 0, 0, 0, 5));
            var ex = Assert.Throws<InvalidOperationException>(() => timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 16, 10)));
            Assert.Equal("Expected SettingRule.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetInvalidCreateReportDataWithMismatchedNetworkStatuses))]
        public async void TestCreateNetworkStatusTimelineWithMismatchedNetworkStatuses(
            IEnumerable<string> networkControllerResultValues,
            IEnumerable<DateTime> networkControllerResultDates,
            IEnumerable<string> networkControllerResultOperations)
        {
            IAsyncEnumerable<TestOperationResult> resultCollection = this.GetStoreTestResultCollection(networkControllerResultValues, networkControllerResultDates, networkControllerResultOperations);
            NetworkStatusTimeline timeline = await NetworkStatusTimeline.CreateAsync(resultCollection.GetAsyncEnumerator(), new TimeSpan(0, 0, 0, 0, 5));
            var ex = Assert.Throws<InvalidOperationException>(() => timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 16, 10)));
            Assert.Equal("Test result SettingRule and following RuleSet do not match NetwokControllerStatuses", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetCreateReportData))]
        public async void TestCreateNetworkStatusTimelineWithIncorrectTestOperationResultType(
            IEnumerable<string> networkControllerResultValues,
            IEnumerable<DateTime> networkControllerResultDates,
            IEnumerable<string> networkControllerResultOperations)
        {
            IAsyncEnumerable<TestOperationResult> resultCollection = this.GetInvalidStoreTestResultCollection(networkControllerResultValues, networkControllerResultDates, networkControllerResultOperations);
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await NetworkStatusTimeline.CreateAsync(resultCollection.GetAsyncEnumerator(), new TimeSpan(0, 0, 0, 0, 5)));
            Assert.Equal("Network Controller Test Results is empty.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(GetInvalidCreateReportDataWithEmptyResults))]
        public async void TestCreateNetworkStatusTimelineWithEmptyInput(
            IEnumerable<string> networkControllerResultValues,
            IEnumerable<DateTime> networkControllerResultDates,
            IEnumerable<string> networkControllerResultOperations)
        {
            IAsyncEnumerable<TestOperationResult> resultCollection = this.GetStoreTestResultCollection(networkControllerResultValues, networkControllerResultDates, networkControllerResultOperations);
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await NetworkStatusTimeline.CreateAsync(resultCollection.GetAsyncEnumerator(), new TimeSpan(0, 0, 0, 0, 5)));
            Assert.Equal("Network Controller Test Results is empty.", ex.Message);
        }

        private StoreTestResultCollection<TestOperationResult> GetStoreTestResultCollection(
            IEnumerable<string> resultValues,
            IEnumerable<DateTime> resultDates,
            IEnumerable<string> resultOperations)
        {
            int batchSize = 500;
            string source = "testSource";
            var expectedStoreData = GetStoreData(source, resultValues, resultDates, resultOperations);
            var mockResultStore = new Mock<ISequentialStore<TestOperationResult>>();
            var resultCollection = new StoreTestResultCollection<TestOperationResult>(mockResultStore.Object, batchSize);
            for (int i = 0; i < expectedStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockResultStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(expectedStoreData.Skip(startingOffset).Take(batchSize));
            }

            return resultCollection;
        }

        private StoreTestResultCollection<TestOperationResult> GetInvalidStoreTestResultCollection(
            IEnumerable<string> resultValues,
            IEnumerable<DateTime> resultDates,
            IEnumerable<string> resultOperations)
        {
            int batchSize = 500;
            string source = "testSource";
            var expectedStoreData = GetInvalidStoreData(source, resultValues, resultDates);
            var mockResultStore = new Mock<ISequentialStore<TestOperationResult>>();
            var resultCollection = new StoreTestResultCollection<TestOperationResult>(mockResultStore.Object, batchSize);
            for (int i = 0; i < expectedStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockResultStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(expectedStoreData.Skip(startingOffset).Take(batchSize));
            }

            return resultCollection;
        }

        static List<(long, TestOperationResult)> GetInvalidStoreData(
            string source,
            IEnumerable<string> resultValues,
            IEnumerable<DateTime> resultDates,
            int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            for (int i = 0; i < resultValues.Count(); i++)
            {
                var networkControllerTestResult = new MessageTestResult(source, resultDates.ElementAt(i));
                storeData.Add((count, networkControllerTestResult.ToTestOperationResult()));
                count++;
            }

            return storeData;
        }

        static List<(long, TestOperationResult)> GetStoreData(
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
    }
}
