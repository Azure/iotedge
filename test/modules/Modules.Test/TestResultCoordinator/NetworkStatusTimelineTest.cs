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
                        new[] { "Enabled", "Disabled", "Enabled", "Disabled" }, new[]
                        {
                            new DateTime(2020, 1, 1, 9, 10, 10, 10),
                            new DateTime(2020, 1, 1, 9, 10, 15, 10),
                            new DateTime(2020, 1, 1, 9, 10, 20, 10),
                            new DateTime(2020, 1, 1, 9, 10, 25, 10)
                        },
                    }
                };
        [Theory]
        [MemberData(nameof(GetCreateReportData))]
        public async void TestCreateNetworkStatusTimeline(IEnumerable<string> networkControllerResultValues, IEnumerable<DateTime> networkControllerResultDates)
        {
            int batchSize = 500;
            string source = "testSource";
            var expectedStoreData = GetStoreData(source, networkControllerResultValues, networkControllerResultDates);
            var mockResultStore = new Mock<ISequentialStore<TestOperationResult>>();
            var resultCollection = new StoreTestResultCollection<TestOperationResult>(mockResultStore.Object, batchSize);
            for (int i = 0; i < expectedStoreData.Count; i += batchSize)
            {
                int startingOffset = i;
                mockResultStore.Setup(s => s.GetBatch(startingOffset, batchSize)).ReturnsAsync(expectedStoreData.Skip(startingOffset).Take(batchSize));
            }

            NetworkStatusTimeline timeline = await NetworkStatusTimeline.CreateNetworkStatusTimeline(resultCollection, 5);
            Assert.Equal(NetworkControllerStatus.Enabled, timeline.GetNetworkControllerStatusAt(new DateTime(2020, 1, 1, 9, 10, 11, 10)));
            Assert.Equal(NetworkControllerStatus.Disabled, timeline.GetNetworkControllerStatusAt(new DateTime(2020, 1, 1, 9, 10, 16, 10)));
            Assert.Equal(NetworkControllerStatus.Enabled, timeline.GetNetworkControllerStatusAt(new DateTime(2020, 1, 1, 9, 10, 22, 10)));
            Assert.Equal(NetworkControllerStatus.Disabled, timeline.GetNetworkControllerStatusAt(new DateTime(2020, 1, 1, 9, 10, 40, 10)));
            Assert.True(timeline.IsWithinTolerancePeriod(new DateTime(2020, 1, 1, 9, 10, 10, 12)));
            Assert.False(timeline.IsWithinTolerancePeriod(new DateTime(2020, 1, 1, 9, 10, 10, 16)));
        }

        static List<(long, TestOperationResult)> GetStoreData(string source, IEnumerable<string> resultValues, IEnumerable<DateTime> resultDates, int start = 0)
        {
            var storeData = new List<(long, TestOperationResult)>();
            int count = start;

            for (int i = 0; i < resultValues.Count(); i++)
            {
                var networkControllerStatus = (NetworkControllerStatus)Enum.Parse(typeof(NetworkControllerStatus), resultValues.ElementAt(i));
                var networkControllerTestResult = new NetworkControllerTestResult(source, resultDates.ElementAt(i)) { NetworkControllerStatus = networkControllerStatus, NetworkControllerType = NetworkControllerType.Offline };
                storeData.Add((count, networkControllerTestResult.ToTestOperationResult()));
                count++;
            }

            return storeData;
        }
    }
}
