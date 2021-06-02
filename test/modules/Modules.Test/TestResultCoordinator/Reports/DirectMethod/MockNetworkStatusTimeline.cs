// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::TestResultCoordinator.Reports;
    using global::TestResultCoordinator.Reports.DirectMethod.Connectivity;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Moq;

    /// <summary>
    /// This is a helper class for DirectMethodReportConnectivityGeneratorTest. It simply creates a static mocked NetworkStatusTimeline.
    /// It's not so easy to mock the timeline, so to avoid clutter in the test class we build the mock here.
    /// </summary>
    class MockNetworkStatusTimeline
    {
        public static async Task<NetworkStatusTimeline> GetMockAsync(TimeSpan tolerancePeriod)
        {
            return await GetNetworkStatusTimelineAsync(
                GetNetworkControllerStoreTestResultCollection(
                    NetworkControllerStatusArray,
                    DateTimeArray,
                    NetworkControllerOperationArray),
                tolerancePeriod);
        }

        static DateTime[] DateTimeArray => new[]
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

        static string[] NetworkControllerStatusArray => new[] { "Disabled", "Disabled", "Enabled", "Enabled", "Disabled", "Disabled", "Enabled", "Enabled" };

        static string[] NetworkControllerOperationArray => new[] { "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet" };

        static async Task<NetworkStatusTimeline> GetNetworkStatusTimelineAsync(IAsyncEnumerable<TestOperationResult> results, TimeSpan tolerancePeriod)
        {
            return await NetworkStatusTimeline.CreateAsync(results.GetAsyncEnumerator(), tolerancePeriod);
        }

        static StoreTestResultCollection<TestOperationResult> GetNetworkControllerStoreTestResultCollection(
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
    }
}
