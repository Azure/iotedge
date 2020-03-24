// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    [EndToEnd]
    public class PriorityQueues : SasManualProvisioningFixture
    {
        [Test]
        public async Task PriorityQueueModuleToModuleMessages()
        {
            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for TempFilter test"));
            string relayerImage = Context.Current.RelayerImage.Expect(() => new ArgumentException("relayerImage parameter is required for TempFilter test"));

            const string trcModuleName = "testResultCoordinator";
            const string loadGenModuleName = "loadGenModule";
            const string relayerModuleName = "relayerModule";
            const string trcUrl = "http://" + trcModuleName + ":5001";

            string trackingId = Guid.NewGuid().ToString();

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
                           ("IOT_HUB_CONNECTION_STRING", this.iotHub.IoTHubConnectionString),
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
                                   ["ExpectedSource"] = "loadGenModule.send",
                                   ["ActualSource"] = "relayerModule.receive",
                                   ["TestDescription"] = "unnecessary"
                               }
                           }
                       });
                    builder.AddModule(loadGenModuleName, loadGenImage)
                        .WithEnvironment(new[]
                        {
                            ("testResultCoordinatorUrl", trcUrl),
                            ("senderType", "PriorityMessageSender"),
                            ("trackingId", "e2eTestTrackingId"),
                            ("testDuration", "00:00:20"),
                            ("messageFrequency", "00:00:01")
                        });

                    builder.GetModule(ModuleName.EdgeHub)
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new Dictionary<string, object>
                            {
                                ["LoadGenToRelayer1"] = new Dictionary<string, object>
                                {
                                    ["route"] = "FROM /messages/modules/" + loadGenModuleName + "/outputs/pri0 INTO BrokeredEndpoint('/modules/" + relayerModuleName + "/inputs/input1')",
                                    ["priority"] = 0
                                },
                                ["LoadGenToRelayer2"] = new Dictionary<string, object>
                                {
                                    ["route"] = "FROM /messages/modules/" + loadGenModuleName + "/outputs/pri1 INTO BrokeredEndpoint('/modules/" + relayerModuleName + "/inputs/input1')",
                                    ["priority"] = 1
                                },
                                ["LoadGenToRelayer3"] = new Dictionary<string, object>
                                {
                                    ["route"] = "FROM /messages/modules/" + loadGenModuleName + "/outputs/pri2 INTO BrokeredEndpoint('/modules/" + relayerModuleName + "/inputs/input1')",
                                    ["priority"] = 2
                                },
                                ["LoadGenToRelayer4"] = new Dictionary<string, object>
                                {
                                    ["route"] = "FROM /messages/modules/" + loadGenModuleName + "/outputs/pri3 INTO BrokeredEndpoint('/modules/" + relayerModuleName + "/inputs/input1')",
                                    ["priority"] = 3
                                }
                            }
                        });
                });

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig, token);

            // Wait for loadGen to send some messages
            await Task.Delay(TimeSpan.FromSeconds(30));

            Action<EdgeConfigBuilder> addRelayerConfig = new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(relayerModuleName, relayerImage)
                        .WithEnvironment(new[] { ("receiveOnly", "true") });
                });

            deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig + addRelayerConfig, token);

            // Wait for relayer to spin up, receive messages, and pass along results to TRC
            await Task.Delay(TimeSpan.FromSeconds(20));

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("http://localhost:5001/api/report");
            var jsonstring = await response.Content.ReadAsStringAsync();
            bool isPassed = (bool)JArray.Parse(jsonstring)[0]["IsPassed"];
            Assert.IsTrue(isPassed);
        }
    }
}
