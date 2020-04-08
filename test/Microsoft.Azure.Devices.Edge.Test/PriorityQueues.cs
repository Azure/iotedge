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
        [Test]
        public async Task PriorityQueueModuleToModuleMessages()
        {
            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            string relayerImage = Context.Current.RelayerImage.Expect(() => new ArgumentException("relayerImage parameter is required for Priority Queues test"));

            const string trcModuleName = "testResultCoordinator";
            const string loadGenModuleName = "loadGenModule";
            const string relayerModuleName = "relayerModule";
            const string trcUrl = "http://" + trcModuleName + ":5001";
            const string loadGenTestDuration = "00:00:20";

            string routeTemplate = $"FROM /messages/modules/{loadGenModuleName}/outputs/pri{0} INTO BrokeredEndpoint('/modules/{relayerModuleName}/inputs/input1')";

            string trackingId = Guid.NewGuid().ToString();
            string priorityString = this.BuildPriorityString(5);
            string ttlString = "0;0;0;0;0";

            Action<EdgeConfigBuilder> addInitialConfig = new Action<EdgeConfigBuilder>(
                builder =>
                {
                    // This test uses the TestResultCoordinator. It was originally designed for connectivity tests, so many of the parameters
                    // are unnecessary for the e2e tests.
                    // TODO: Make TestResultCoordinator more generic, so we don't have to fill out garbage values in the e2e tests.
                    builder.AddModule(trcModuleName, trcImage)
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
                                   ["ExpectedSource"] = $"{loadGenModuleName}.send",
                                   ["ActualSource"] = $"{relayerModuleName}.receive",
                                   ["TestDescription"] = "unnecessary"
                               }
                           }
                       });

                    builder.AddModule(loadGenModuleName, loadGenImage)
                        .WithEnvironment(new[]
                        {
                            ("testResultCoordinatorUrl", trcUrl),
                            ("senderType", "PriorityMessageSender"),
                            ("trackingId", trackingId),
                            ("testDuration", loadGenTestDuration),
                            ("messageFrequency", "00:00:00.5"),
                            ("priorities", priorityString),
                            ("ttls", ttlString),
                            ("ttlThreshold", "100")
                        });

                    Dictionary<string, object> routes = this.BuildRoutes(priorityString.Split(';'), ttlString.Split(";"), loadGenModuleName, relayerModuleName);
                    builder.GetModule(ModuleName.EdgeHub).WithDesiredProperties(new Dictionary<string, object> { ["routes"] = routes });
                });

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig, token);
            PriorityQueueTestStatus loadGenTestStatus = await this.PollUntilFinishedAsync(loadGenModuleName, token);

            Action<EdgeConfigBuilder> addRelayerConfig = new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(relayerModuleName, relayerImage)
                        .WithEnvironment(new[]
                        {
                            ("receiveOnly", "true"),
                            ("uniqueResultsExpected", loadGenTestStatus.ResultCount.ToString())
                        });
                });

            deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig + addRelayerConfig, token, false);
            await this.PollUntilFinishedAsync(relayerModuleName, token);

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

        [Test]

        public async Task PriorityQueueTimeToLive()
        {
            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            string relayerImage = Context.Current.RelayerImage.Expect(() => new ArgumentException("relayerImage parameter is required for Priority Queues test"));

            const string trcModuleName = "testResultCoordinator";
            const string loadGenModuleName = "loadGenModule";
            const string relayerModuleName = "relayerModule";
            const string trcUrl = "http://" + trcModuleName + ":5001";
            const string loadGenTestDuration = "00:00:20";

            string routeTemplate = $"FROM /messages/modules/{loadGenModuleName}/outputs/pri{0} INTO BrokeredEndpoint('/modules/{relayerModuleName}/inputs/input1')";

            string trackingId = Guid.NewGuid().ToString();
            string priorityString = this.BuildPriorityString(5);
            string ttlString = this.BuildTTLString(5);
            int ttlThresholdSecs = 15;

            Action<EdgeConfigBuilder> addInitialConfig = new Action<EdgeConfigBuilder>(
                builder =>
                {
                    // This test uses the TestResultCoordinator. It was originally designed for connectivity tests, so many of the parameters
                    // are unnecessary for the e2e tests.
                    // TODO: Make TestResultCoordinator more generic, so we don't have to fill out garbage values in the e2e tests.
                    builder.AddModule(trcModuleName, trcImage)
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
                                   ["ExpectedSource"] = $"{loadGenModuleName}.send",
                                   ["ActualSource"] = $"{relayerModuleName}.receive",
                                   ["TestDescription"] = "unnecessary"
                               }
                           }
                       });

                    builder.AddModule(loadGenModuleName, loadGenImage)
                        .WithEnvironment(new[]
                        {
                            ("testResultCoordinatorUrl", trcUrl),
                            ("senderType", "PriorityMessageSender"),
                            ("trackingId", trackingId),
                            ("testDuration", loadGenTestDuration),
                            ("messageFrequency", "00:00:00.5"),
                            ("priorities", priorityString),
                            ("ttls", ttlString),
                            ("ttlThreshold", ttlThresholdSecs.ToString())
                        });

                    Dictionary<string, object> routes = this.BuildRoutes(priorityString.Split(';'), ttlString.Split(";"), loadGenModuleName, relayerModuleName);
                    builder.GetModule(ModuleName.EdgeHub).WithDesiredProperties(new Dictionary<string, object> { ["routes"] = routes });
                });

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig, token);
            PriorityQueueTestStatus loadGenTestStatus = await this.PollUntilFinishedAsync(loadGenModuleName, token);

            // Wait long enough for TTL to expire for some of the messages
            Log.Information("Waiting for 20 seconds for TTL's to expire");
            await Task.Delay(20000);

            Action<EdgeConfigBuilder> addRelayerConfig = new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(relayerModuleName, relayerImage)
                        .WithEnvironment(new[]
                        {
                            ("receiveOnly", "true"),
                            ("uniqueResultsExpected", loadGenTestStatus.ResultCount.ToString())
                        });
                });

            deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig + addRelayerConfig, token, false);
            await this.PollUntilFinishedAsync(relayerModuleName, token);

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

        private Dictionary<string, object> BuildRoutes(string[] priorities, string[] ttls, string sendModule, string receiveModule)
        {
            Dictionary<string, object> routes = new Dictionary<string, object>();
            for (int i = 0; i < priorities.Length; i++)
            {
                string ttl = ttls[i];
                string priority = priorities[i];
                // If we encounter "Default" in the priority list, don't add a priority - the default priority will automatically get picked up
                var routeInfo = new Dictionary<string, object>();
                routeInfo["route"] = $"FROM /messages/modules/{sendModule}/outputs/pri{priority} INTO BrokeredEndpoint('/modules/{receiveModule}/inputs/input1')";
                routeInfo["timeToLiveSecs"] = int.Parse(ttl);
                if (!priority.Contains(TestConstants.PriorityQueues.Default))
                {
                    routeInfo["priority"] = int.Parse(priority);
                }
                if (int.Parse(ttl) != -1)
                {
                    routeInfo["timeToLiveSecs"] = int.Parse(ttl);
                }
                routes.Add($"LoadGenToRelayer{priority}", routeInfo);
            }

            return routes;
        }

        private string BuildTTLString(int numberOfTTLs)
        {
            Random rng = new Random();
            // Choose from a set of 2 TTLs below the threshold, 2 above, default TTL of 0, and no TTL denoted by -1
            var ttlSet = new string[] { "5", "10", "400", "1600", "0", "-1" };

            // Make sure default is always in the string. We always want to test that default TTL works.
            string ttlString = "0;";
            for (int i = 0; i < numberOfTTLs-1; i++)
            {
                ttlString = ttlString + ttlSet[rng.Next(ttlSet.Length)];
            }

            // Return a string with two TTL's above the threshold, two TTL's below the threshold, and one that is default in a random order
            return  string.Join(
                ";",
                new string[] { "5", "10", "400", "1600", "0" }
                    .OrderBy(x => rng.Next())
                    .ToArray());
        }

        private string BuildPriorityString(int numberOfPriorities)
        {
            if (numberOfPriorities > 11)
            {
                throw new ArgumentException("Maximum of 11 priorities (10 priorities + the Default priority) is supported at this time.");
            }

            string priorityString = string.Empty;
            Random rng = new Random();
            for (int i = 0; i < numberOfPriorities - 1; i++)
            {
                string pri;
                do
                {
                    pri = rng.Next(10).ToString();
                }
                while (priorityString.Contains(pri));

                priorityString = priorityString + pri + ";";
            }

            return priorityString + TestConstants.PriorityQueues.Default;
        }

        private async Task<PriorityQueueTestStatus> PollUntilFinishedAsync(string moduleName, CancellationToken token)
        {
            PriorityQueueTestStatus testStatus;
            do
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                var result = await this.iotHub.InvokeMethodAsync(Context.Current.DeviceId, moduleName, new CloudToDeviceMethod("IsFinished", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300)), token);
                Assert.AreEqual(result.Status, (int)HttpStatusCode.OK);
                testStatus = JsonConvert.DeserializeObject<PriorityQueueTestStatus>(result.GetPayloadAsJson());
            }
            while (!testStatus.IsFinished);
            return testStatus;
        }
    }
}
