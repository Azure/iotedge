// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
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
        const string NetworkControllerModuleName = "networkController";
        const string TrcUrl = "http://" + TrcModuleName + ":5001";
        const string LoadGenTestDuration = "00:00:20";
        const string DefaultLoadGenTestStartDelay = "00:00:20";

        [Test]
        [Category("FlakyOnArm")]
        public async Task PriorityQueueModuleToModuleMessages()
        {
            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            string relayerImage = Context.Current.RelayerImage.Expect(() => new ArgumentException("relayerImage parameter is required for Priority Queues test"));
            string trackingId = Guid.NewGuid().ToString();
            TestInfo testInfo = this.InitTestInfo(5, 1000, true);

            Action<EdgeConfigBuilder> addLoadGenConfig = this.BuildAddLoadGenConfig(trackingId, loadGenImage, testInfo, false);
            Action<EdgeConfigBuilder> addTrcConfig = TestResultCoordinatorUtil.BuildAddTestResultCoordinatorConfig(trackingId, trcImage, LoadGenModuleName, RelayerModuleName);

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addLoadGenConfig + addTrcConfig, token, Context.Current.NestedEdge);
            PriorityQueueTestStatus loadGenTestStatus = await this.PollUntilFinishedAsync(LoadGenModuleName, token);
            Action<EdgeConfigBuilder> addRelayerConfig = this.BuildAddRelayerConfig(relayerImage, loadGenTestStatus);
            deployment = await this.runtime.DeployConfigurationAsync(addLoadGenConfig + addTrcConfig + addRelayerConfig, token, Context.Current.NestedEdge);
            await this.PollUntilFinishedAsync(RelayerModuleName, token);
            Assert.True(await TestResultCoordinatorUtil.IsResultValidAsync());
        }

        [Test]
        [Category("Flaky")]
        public async Task PriorityQueueModuleToHubMessages()
        {
            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            string relayerImage = Context.Current.RelayerImage.Expect(() => new ArgumentException("relayerImage parameter is required for Priority Queues test"));
            string networkControllerImage = Context.Current.NetworkControllerImage.Expect(() => new ArgumentException("networkControllerImage parameter is required for Priority Queues test"));
            string trackingId = Guid.NewGuid().ToString();
            TestInfo testInfo = this.InitTestInfo(5, 1000, true, "00:00:40");

            var testResultReportingClient = new TestResultReportingClient { BaseUrl = "http://localhost:5001" };

            Action<EdgeConfigBuilder> addLoadGenConfig = this.BuildAddLoadGenConfig(trackingId, loadGenImage, testInfo, true);
            Action<EdgeConfigBuilder> addTrcConfig = TestResultCoordinatorUtil.BuildAddTestResultCoordinatorConfig(trackingId, trcImage, LoadGenModuleName, "hubtest");
            Action<EdgeConfigBuilder> addNetworkControllerConfig = TestResultCoordinatorUtil.BuildAddNetworkControllerConfig(trackingId, networkControllerImage);

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addLoadGenConfig + addTrcConfig + addNetworkControllerConfig, token, Context.Current.NestedEdge);
            bool networkOn = true;
            await this.ToggleConnectivity(!networkOn, NetworkControllerModuleName, token);
            await Task.Delay(TimeSpan.Parse(LoadGenTestDuration) + TimeSpan.Parse(testInfo.LoadGenStartDelay) + TimeSpan.FromSeconds(10));
            await this.ToggleConnectivity(networkOn, NetworkControllerModuleName, token);
            PriorityQueueTestStatus loadGenTestStatus = await this.PollUntilFinishedAsync(LoadGenModuleName, token);
            ConcurrentQueue<MessageTestResult> messages = new ConcurrentQueue<MessageTestResult>();
            await this.ReceiveEventsFromIotHub(deployment.StartTime, messages, loadGenTestStatus, trackingId, token);
            while (messages.TryDequeue(out MessageTestResult messageTestResult))
            {
                await testResultReportingClient.ReportResultAsync(messageTestResult.ToTestOperationResultDto());
            }

            Assert.True(await TestResultCoordinatorUtil.IsResultValidAsync());
        }

        [Test]
        [Category("FlakyOnArm")]
        public async Task PriorityQueueTimeToLive()
        {
            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            string relayerImage = Context.Current.RelayerImage.Expect(() => new ArgumentException("relayerImage parameter is required for Priority Queues test"));
            string trackingId = Guid.NewGuid().ToString();
            TestInfo testInfo = this.InitTestInfo(5, 20);

            Action<EdgeConfigBuilder> addLoadGenConfig = this.BuildAddLoadGenConfig(trackingId, loadGenImage, testInfo, false);
            Action<EdgeConfigBuilder> addTrcConfig = TestResultCoordinatorUtil.BuildAddTestResultCoordinatorConfig(trackingId, trcImage, LoadGenModuleName, RelayerModuleName);

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addLoadGenConfig + addTrcConfig, token, Context.Current.NestedEdge);
            PriorityQueueTestStatus loadGenTestStatus = await this.PollUntilFinishedAsync(LoadGenModuleName, token);

            await Profiler.Run(
                () => Task.Delay(testInfo.TtlThreshold * 1000),
                "Waited for message TTL to expire");

            Action<EdgeConfigBuilder> addRelayerConfig = this.BuildAddRelayerConfig(relayerImage, loadGenTestStatus);
            deployment = await this.runtime.DeployConfigurationAsync(addLoadGenConfig + addTrcConfig + addRelayerConfig, token, Context.Current.NestedEdge);
            await this.PollUntilFinishedAsync(RelayerModuleName, token);
            Assert.True(await TestResultCoordinatorUtil.IsResultValidAsync());
        }

        async Task ReceiveEventsFromIotHub(DateTime startTime, ConcurrentQueue<MessageTestResult> messages, PriorityQueueTestStatus loadGenTestStatus, string trackingId, CancellationToken token)
        {
            await Profiler.Run(
                async () =>
                {
                    HashSet<int> results = new HashSet<int>();
                    await this.IotHub.ReceiveEventsAsync(
                        this.runtime.DeviceId,
                        startTime,
                        data =>
                        {
                            if (data.Properties.ContainsKey("trackingId") &&
                                data.Properties.ContainsKey("batchId") &&
                                data.Properties.ContainsKey("sequenceNumber"))
                            {
                                int sequenceNumber = int.Parse(data.Properties["sequenceNumber"].ToString());
                                Log.Verbose($"Received message from IoTHub with sequence number: {sequenceNumber}");

                                var receivedTrackingId = (string)data.Properties["trackingId"];
                                if (!receivedTrackingId.IsNullOrWhiteSpace() && receivedTrackingId.Equals(trackingId))
                                {
                                    messages.Enqueue(new MessageTestResult("hubtest.receive", DateTime.UtcNow)
                                    {
                                        TrackingId = data.Properties["trackingId"].ToString(),
                                        BatchId = data.Properties["batchId"].ToString(),
                                        SequenceNumber = data.Properties["sequenceNumber"].ToString()
                                    });
                                    results.Add(sequenceNumber);
                                }
                                else
                                {
                                    var message = receivedTrackingId.IsNullOrWhiteSpace() ? "EMPTY" : receivedTrackingId;
                                    Log.Verbose($"Message contains incorrect tracking id: {message}. Ignoring.");
                                }
                            }
                            else
                            {
                                Log.Warning("Message is missing information. Needs to have trackingId, batchId, and sequenceNumber. Not enqueuing.");
                            }

                            return results.Count == loadGenTestStatus.ResultCount;
                        },
                        token);
                },
                "Received {ResultCount} unique results from IoTHub",
                loadGenTestStatus.ResultCount);
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

        private Action<EdgeConfigBuilder> BuildAddNetworkControllerConfig(string trackingId, string networkControllerImage)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(NetworkControllerModuleName, networkControllerImage)
                        .WithEnvironment(new[]
                        {
                            ("trackingId", trackingId),
                            ("testResultCoordinatorUrl", TrcUrl),
                            ("RunFrequencies__0__OfflineFrequency", "00:00:00"),
                            ("RunFrequencies__0__OnlineFrequency", "00:00:00"),
                            ("RunFrequencies__0__RunsCount", "0"),
                            ("NetworkControllerRunProfile", "Online"),
                            ("StartAfter", "00:00:00")
                        })
                        .WithSettings(new[] { ("createOptions", "{\"HostConfig\":{\"Binds\":[\"/var/run/docker.sock:/var/run/docker.sock\"], \"NetworkMode\":\"host\", \"Privileged\":true},\"NetworkingConfig\":{\"EndpointsConfig\":{\"host\":{}}}}") });
                });
        }

        private Action<EdgeConfigBuilder> BuildAddLoadGenConfig(string trackingId, string loadGenImage, TestInfo testInfo, bool cloudUpstream)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(LoadGenModuleName, loadGenImage)
                        .WithEnvironment(new[]
                        {
                            ("testResultCoordinatorUrl", TrcUrl),
                            ("senderType", "PriorityMessageSender"),
                            ("testStartDelay", testInfo.LoadGenStartDelay),
                            ("trackingId", trackingId),
                            ("testDuration", LoadGenTestDuration),
                            ("messageFrequency", "00:00:00.5"),
                            ("priorities", string.Join(';', testInfo.Priorities)),
                            ("ttls", string.Join(';', testInfo.Ttls)),
                            ("ttlThresholdSecs", testInfo.TtlThreshold.ToString())
                        });

                    Dictionary<string, object> routes = this.BuildRoutes(testInfo, LoadGenModuleName, RelayerModuleName, cloudUpstream);
                    builder.GetModule(ModuleName.EdgeHub).WithDesiredProperties(new Dictionary<string, object> { ["routes"] = routes });
                });
        }

        private Dictionary<string, object> BuildRoutes(TestInfo testInfo, string sendModule, string receiveModule, bool cloudUpstream)
        {
            Dictionary<string, object> routes = new Dictionary<string, object>();
            for (int i = 0; i < testInfo.Priorities.Count; i++)
            {
                int ttl = testInfo.Ttls[i];
                int priority = testInfo.Priorities[i];
                var routeInfo = new Dictionary<string, object>();
                routeInfo["route"] = cloudUpstream ?
                    $"FROM /messages/modules/{sendModule}/outputs/pri{priority} INTO $upstream"
                    : $"FROM /messages/modules/{sendModule}/outputs/pri{priority} INTO BrokeredEndpoint('/modules/{receiveModule}/inputs/input1')";

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

        private async Task ToggleConnectivity(bool connectivityOn, string moduleName, CancellationToken token) =>
            await Profiler.Run(
                async () =>
                    await this.IotHub.InvokeMethodAsync(
                        this.runtime.DeviceId,
                        moduleName,
                        new CloudToDeviceMethod("toggleConnectivity", TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20)).SetPayloadJson($"{{\"networkOnValue\": \"{connectivityOn}\"}}"),
                        token),
                "Toggled connectivity to {Connectivity}",
                connectivityOn ? "on" : "off");

        private async Task<PriorityQueueTestStatus> PollUntilFinishedAsync(string moduleName, CancellationToken token)
        {
            PriorityQueueTestStatus testStatus;
            do
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                var result = await this.IotHub.InvokeMethodAsync(this.runtime.DeviceId, moduleName, new CloudToDeviceMethod("IsFinished", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)), token);
                Assert.AreEqual(result.Status, (int)HttpStatusCode.OK);
                testStatus = JsonConvert.DeserializeObject<PriorityQueueTestStatus>(result.GetPayloadAsJson());
            }
            while (!testStatus.IsFinished);
            return testStatus;
        }

        private TestInfo InitTestInfo(int numOfRoutes, int ttlThreshold, bool defaultTtls = false, string loadGenDelay = DefaultLoadGenTestStartDelay)
        {
            List<int> ttls = defaultTtls ? new List<int>(Enumerable.Repeat(0, numOfRoutes)) : this.BuildTtls(numOfRoutes, ttlThreshold);
            List<int> priorities = this.BuildPriorities(numOfRoutes);
            return new TestInfo() { Ttls = ttls, Priorities = priorities, TtlThreshold = ttlThreshold, LoadGenStartDelay = loadGenDelay };
        }

        struct TestInfo
        {
            public List<int> Ttls;
            public List<int> Priorities;
            public int TtlThreshold;
            public string LoadGenStartDelay;
        }
    }
}
