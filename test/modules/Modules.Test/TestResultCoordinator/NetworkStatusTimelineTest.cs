// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::TestResultCoordinator;
    using global::TestResultCoordinator.Report.DirectMethodReport;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Moq;
    using Xunit;

    public class NetworkStatusTimelineTest
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            new List<object[]>
                {
                    new object[]
                    {
                        new[] { "Enabled", "Enabled", "Disabled", "Disabled", "Enabled", "Enabled", "Disabled", "Disabled" },
                        new[]
                        {
                            new DateTime(2020, 1, 1, 9, 10, 10, 10),
                            new DateTime(2020, 1, 1, 9, 10, 10, 13),
                            new DateTime(2020, 1, 1, 9, 10, 15, 10),
                            new DateTime(2020, 1, 1, 9, 10, 15, 13),
                            new DateTime(2020, 1, 1, 9, 10, 20, 10),
                            new DateTime(2020, 1, 1, 9, 10, 20, 13),
                            new DateTime(2020, 1, 1, 9, 10, 25, 10),
                            new DateTime(2020, 1, 1, 9, 10, 25, 13)
                        },
                        new[] { "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet", "SettingRule", "RuleSet" }
                    }
                };
        [Theory]
        [MemberData(nameof(GetCreateReportData))]
        public async void TestCreateNetworkStatusTimeline(
            IEnumerable<string> networkControllerResultValues,
            IEnumerable<DateTime> networkControllerResultDates,
            IEnumerable<string> networkControllerResultOperations)
        {
            int batchSize = 500;
            string source = "testSource";
            var expectedStoreData = GetStoreData(source, networkControllerResultValues, networkControllerResultDates, networkControllerResultOperations);
            var mockResultStore = new Mock<ISequentialStore<TestOperationResult>>();
            var resultCollection = new StoreTestResultCollection<TestOperationResult>(mockResultStore.Object, batchSize);
            for (int i = 0; i < expectedStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockResultStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(expectedStoreData.Skip(startingOffset).Take(batchSize));
            }

            NetworkStatusTimeline timeline = await NetworkStatusTimeline.Create(resultCollection, new TimeSpan(0, 0, 0, 0, 5));

            (NetworkControllerStatus status, bool inTolerance) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 11, 10));
            Assert.Equal(NetworkControllerStatus.Enabled, status);
            Assert.False(inTolerance);
            (status, inTolerance) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 16, 10));
            Assert.Equal(NetworkControllerStatus.Disabled, status);
            Assert.False(inTolerance);
            (status, inTolerance) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 20, 15));
            Assert.Equal(NetworkControllerStatus.Enabled, status);
            Assert.True(inTolerance);
            (status, inTolerance) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 25, 10));
            Assert.Equal(NetworkControllerStatus.Disabled, status);
            Assert.True(inTolerance);
            (status, inTolerance) = timeline.GetNetworkControllerStatusAndWithinToleranceAt(new DateTime(2020, 1, 1, 9, 10, 25, 12));
            Assert.Equal(NetworkControllerStatus.Disabled, status);
            Assert.True(inTolerance);
        }

        static List<(long, TestOperationResult)> GetStoreData(string source, IEnumerable<string> resultValues, IEnumerable<DateTime> resultDates, IEnumerable<string> resultOperations, int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            for (int i = 0; i < resultValues.Count(); i++)
            {
                var networkControllerStatus = (NetworkControllerStatus)Enum.Parse(typeof(NetworkControllerStatus), resultValues.ElementAt(i));
                var networkControllerOperation = (NetworkControllerOperation)Enum.Parse(typeof(NetworkControllerOperation), resultOperations.ElementAt(i));
                var networkControllerTestResult = new NetworkControllerTestResult(source, resultDates.ElementAt(i)) { NetworkControllerStatus = networkControllerStatus, NetworkControllerType = NetworkControllerType.Offline, Operation = networkControllerOperation };
                storeData.Add((count, networkControllerTestResult.ToTestOperationResult()));
                count++;
            }

            return storeData;
        }
    }
}
