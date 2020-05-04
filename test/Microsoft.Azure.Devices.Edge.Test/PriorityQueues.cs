// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    public class PriorityQueues : SasManualProvisioningFixture
    {
        const string TrcModuleName = "testResultCoordinator";
        const string LoadGenModuleName = "loadGenModule";
        const string RelayerModuleName = "relayerModule";
        const string TrcUrl = "http://" + TrcModuleName + ":5001";
        const string LoadGenTestDuration = "00:00:20";

        [Test]
        public async Task PriorityQueueModuleToModuleMessages()
        {
            // TODO: Fix PriorityQueue E2E tests for Windows and ARM32
            if (OsPlatform.IsWindows() || !OsPlatform.Is64Bit() )
            {
                Assert.Ignore("Priority Queue module to module messages test has been disabled for Windows and Arm32 until we can fix it.");
            }

            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            string relayerImage = Context.Current.RelayerImage.Expect(() => new ArgumentException("relayerImage parameter is required for Priority Queues test"));
            string trackingId = Guid.NewGuid().ToString();
            TestInfo testInfo = this.InitTestInfo(5, 1000, true);

            Action<EdgeConfigBuilder> addInitialConfig = this.BuildAddInitialConfig(trackingId, trcImage, loadGenImage, testInfo);
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig, token);
            PriorityQueueTestStatus loadGenTestStatus = await this.PollUntilFinishedAsync(LoadGenModuleName, token);
            Action<EdgeConfigBuilder> addRelayerConfig = this.BuildAddRelayerConfig(relayerImage, loadGenTestStatus);
            deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig + addRelayerConfig, token, false);
            await this.PollUntilFinishedAsync(RelayerModuleName, token);
            await this.ValidateResultsAsync();
        }

        [Test]
        public async Task PriorityQueueTimeToLive()
        {
            // TODO: Fix PriorityQueue TTL E2E tests for Windows and ARM32
            if (OsPlatform.IsWindows() || !OsPlatform.Is64Bit())
            {
                Assert.Ignore("Priority Queue time to live test has been disabled for Windows and Arm32 until we can fix it.");
            }

            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            string relayerImage = Context.Current.RelayerImage.Expect(() => new ArgumentException("relayerImage parameter is required for Priority Queues test"));
            string trackingId = Guid.NewGuid().ToString();
            TestInfo testInfo = this.InitTestInfo(5, 20);

            Action<EdgeConfigBuilder> addInitialConfig = this.BuildAddInitialConfig(trackingId, trcImage, loadGenImage, testInfo);
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig, token);
            PriorityQueueTestStatus loadGenTestStatus = await this.PollUntilFinishedAsync(LoadGenModuleName, token);

            // Wait long enough for TTL to expire for some of the messages
            Log.Information($"Waiting for {testInfo.TtlThreshold} seconds for TTL's to expire");
            await Task.Delay(testInfo.TtlThreshold * 1000);

            Action<EdgeConfigBuilder> addRelayerConfig = this.BuildAddRelayerConfig(relayerImage, loadGenTestStatus);
            deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig + addRelayerConfig, token, false);
            await this.PollUntilFinishedAsync(RelayerModuleName, token);
            await this.ValidateResultsAsync();
        }

        private async Task ValidateResultsAsync()
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("http://localhost:5001/api/report");
            var jsonstring = await response.Content.ReadAsStringAsync();
            bool isPassed = (bool)JArray.Parse(jsonstring)[0]["IsPassed"];
            if (!isPassed)
            {
                Log.Verbose("Test Result Coordinator response: {Response}", jsonstring);
            }

            Assert.IsTrue(isPassed);
        }

        private Action<EdgeConfigBuilder> BuildAddRelayerConfig(string relayerImage, PriorityQueueTestStatus loadGenTestStatus)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(RelayerModuleName, relayerImage)
                        .WithEnvironment(new[]
                        {
                            ("receiveOnly", "true"),
                            ("uniqueResultsExpected", loadGenTestStatus.ResultCount.ToString()),
                            ("testResultCoordinatorUrl", TrcUrl)
                        });
                });
        }

        private Action<EdgeConfigBuilder> BuildAddInitialConfig(string trackingId, string trcImage, string loadGenImage, TestInfo testInfo)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    // This test uses the TestResultCoordinator. It was originally designed for connectivity tests, so many of the parameters
                    // are unnecessary for the e2e tests.
                    // TODO: Make TestResultCoordinator more generic, so we don't have to fill out garbage values in the e2e tests.
                    builder.AddModule(TrcModuleName, trcImage)
                       .WithEnvironment(new[]
                       {
                           ("trackingId", trackingId),
                           ("eventHubConnectionString", "Unnecessary"),
                           ("IOT_HUB_CONNECTION_STRING", Context.Current.ConnectionString),
                           ("logAnalyticsWorkspaceId", "Unnecessary"),
                           ("logAnalyticsSharedKey", "Unnecessary"),
                           ("logAnalyticsLogType", "Unnecessary"),
                           ("testStartDelay", "00:00:00"),
                           ("testDuration", "00:20:00"),
                           ("verificationDelay", "00:00:00"),
                           ("STORAGE_ACCOUNT_CONNECTION_STRING", "Unnecessary"),
                           ("NetworkControllerRunProfile", "Online"),
                           ("TEST_INFO", "key=unnecessary")
                       })
                       .WithSettings(new[] { ("createOptions", "{\"HostConfig\": {\"PortBindings\": {\"5001/tcp\": [{\"HostPort\": \"5001\"}]}}}") })

                       .WithDesiredProperties(new Dictionary<string, object>
                       {
                           ["reportMetadataList"] = new Dictionary<string, object>
                           {
                               ["reportMetadata1"] = new Dictionary<string, object>
                               {
                                   ["TestReportType"] = "CountingReport",
                                   ["TestOperationResultType"] = "Messages",
                                   ["ExpectedSource"] = $"{LoadGenModuleName}.send",
                                   ["ActualSource"] = $"{RelayerModuleName}.receive",
                                   ["TestDescription"] = "unnecessary"
                               }
                           }
                       });

                    builder.AddModule(LoadGenModuleName, loadGenImage)
                        .WithEnvironment(new[]
                        {
                            ("testResultCoordinatorUrl", TrcUrl),
                            ("senderType", "PriorityMessageSender"),
                            ("trackingId", trackingId),
                            ("testDuration", LoadGenTestDuration),
                            ("messageFrequency", "00:00:00.5"),
                            ("priorities", string.Join(';', testInfo.Priorities)),
                            ("ttls", string.Join(';', testInfo.Ttls)),
                            ("ttlThresholdSecs", testInfo.TtlThreshold.ToString())
                        });

                    Dictionary<string, object> routes = this.BuildRoutes(testInfo, LoadGenModuleName, RelayerModuleName);
                    builder.GetModule(ModuleName.EdgeHub).WithDesiredProperties(new Dictionary<string, object> { ["routes"] = routes });
                });
        }

        private Dictionary<string, object> BuildRoutes(TestInfo testInfo, string sendModule, string receiveModule)
        {
            Dictionary<string, object> routes = new Dictionary<string, object>();
            for (int i = 0; i < testInfo.Priorities.Count; i++)
            {
                int ttl = testInfo.Ttls[i];
                int priority = testInfo.Priorities[i];
                var routeInfo = new Dictionary<string, object>();
                routeInfo["route"] = $"FROM /messages/modules/{sendModule}/outputs/pri{priority} INTO BrokeredEndpoint('/modules/{receiveModule}/inputs/input1')";

                // If we encounter "Default" in the priority list, don't add a priority - the default priority will automatically get picked up
                if (priority >= 0)
                {
                    routeInfo["priority"] = priority;
                }

                if (ttl >= 0)
                {
                    routeInfo["timeToLiveSecs"] = ttl;
                }

                routes.Add($"LoadGenToRelayer{priority}", routeInfo);
            }

            return routes;
        }

        private List<int> BuildTtls(int numberOfTTLs, int ttlThreshold)
        {
            Random rng = new Random();
            // Choose from a set of hardcoded offsets to add to the threshold, some below, some above
            var ttlOffsets = new int[] { -15, -5, 200, 1600 };

            // Make sure default is always in the string. We always want to test that default TTL works.
            List<int> ttls = new List<int>() { -1, 0 };
            for (int i = 0; i < numberOfTTLs - 2; i++)
            {
                ttls.Add(ttlThreshold + ttlOffsets[rng.Next(ttlOffsets.Length)]);
            }

            return ttls;
        }

        private List<int> BuildPriorities(int numberOfPriorities)
        {
            if (numberOfPriorities > 11)
            {
                throw new ArgumentException("Maximum of 11 priorities (10 priorities + the Default priority) is supported at this time.");
            }

            // -1 represents default priority, which we always want to test
            List<int> priorities = new List<int>() { -1 };

            Random rng = new Random();
            for (int i = 0; i < numberOfPriorities - 1; i++)
            {
                int pri;
                do
                {
                    pri = rng.Next(10);
                }
                while (priorities.Contains(pri));

                priorities.Add(pri);
            }

            return priorities;
        }

        private async Task<PriorityQueueTestStatus> PollUntilFinishedAsync(string moduleName, CancellationToken token)
        {
            PriorityQueueTestStatus testStatus;
            do
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                var result = await this.iotHub.InvokeMethodAsync(this.runtime.DeviceId, moduleName, new CloudToDeviceMethod("IsFinished", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)), token);
                Assert.AreEqual(result.Status, (int)HttpStatusCode.OK);
                testStatus = JsonConvert.DeserializeObject<PriorityQueueTestStatus>(result.GetPayloadAsJson());
            }
            while (!testStatus.IsFinished);
            return testStatus;
        }

        private TestInfo InitTestInfo(int numOfRoutes, int ttlThreshold, bool defaultTtls = false)
        {
            List<int> ttls = defaultTtls ? new List<int>(Enumerable.Repeat(0, numOfRoutes)) : this.BuildTtls(numOfRoutes, ttlThreshold);
            List<int> priorities = this.BuildPriorities(numOfRoutes);
            return new TestInfo() { Ttls = ttls, Priorities = priorities, TtlThreshold = ttlThreshold };
        }

        struct TestInfo
        {
            public List<int> Ttls;
            public List<int> Priorities;
            public int TtlThreshold;
        }
    }
}
