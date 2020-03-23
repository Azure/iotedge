// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using TestResultCoordinator.Reports;

    [EndToEnd]
    public class PriorityQueues : SasManualProvisioningFixture
    {
        // Here, put an end to end test for priority queues.
        // End to end will look something like this:
        // Start up edge with Sender module (load gen would work well)
        // Wait a bit while the sender module sends messages
        // Start up a receiver module
        // Verify that the receiver module received the messages correctly

        private sealed class TestResultCoordinator
        {
            public string Name { get; }
            public string Image { get; }

            private const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";
            private static int instanceCount = 0;

            private TestResultCoordinator(int number)
            {
                this.Name = "tempSensor" + number.ToString();
                this.Image = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
            }

            public static TestResultCoordinator GetInstance()
            {
                return new TestResultCoordinator(TestResultCoordinator.instanceCount++);
            }
        }

        [Test]
        public async Task PriorityQueueModuleToModuleMessages()
        {
            CancellationToken token = this.TestToken;
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Priority Queues test"));
            string loadGenImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("loadGenImage parameter is required for TempFilter test"));
            string relayerImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("relayerImage parameter is required for TempFilter test"));

            const string trcModuleName = "testResultCoordinator";
            const string loadGenModuleName = "loadGenModule";
            const string relayerModuleName = "relayerModule";
            const string trcUrl = "http://" + trcModuleName + ":5001";

            Action<EdgeConfigBuilder> addInitialConfig = new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(trcModuleName, trcImage)
                       .WithEnvironment(new[]
                       {
                           ("trackingId", Guid.NewGuid().ToString()),
                           ("eventHubConnectionString", "Unnecessary value for e2e test"),
                           ("IOT_HUB_CONNECTION_STRING", "HostName=dybronso-iot-hub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=g8BRuiPbFRLEttMsncI6aHUw21Jjr+AEb/Yf4brYD7Y="),
                           ("logAnalyticsWorkspaceId", "Unnecessary value for e2e test"),
                           ("logAnalyticsSharedKey", "Unnecessary value for e2e test"),
                           ("testStartDelay", "00:00:00"),
                           ("testDuration", "01:00:00"),
                           ("verificationDelay", "00:00:10"),
                           ("STORAGE_ACCOUNT_CONNECTION_STRING", "Unnecessary value for e2e test"),
                           ("NetworkControllerRunProfile", "unnecessary"),
                           ("TEST_INFO", "garbage value")
                       })

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
                                   ["TestDescription"] = "this field isn't used by TRC for E2E tests"
                               }
                           }
                       });
                    builder.AddModule(loadGenModuleName, loadGenImage)
                        .WithEnvironment(new[]
                        {
                            ("testResultCoordinatorUrl", trcUrl),
                            ("senderType", "PriorityMessageSender")
                        });

            builder.GetModule(ModuleName.EdgeHub)
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new
                            {
                                LoadGenToRelayer1 = "FROM /messages/modules/" + loadGenModuleName + "/outputs/pri0 INTO BrokeredEndpoint('/modules/" + relayerModuleName + "/inputs/input1')",
                                LoadGenToRelayer2 = "FROM /messages/modules/" + loadGenModuleName + "/outputs/pri1 INTO BrokeredEndpoint('/modules/" + relayerModuleName + "/inputs/input1')",
                                LoadGenToRelayer3 = "FROM /messages/modules/" + loadGenModuleName + "/outputs/pri2 INTO BrokeredEndpoint('/modules/" + relayerModuleName + "/inputs/input1')",
                                LoadGenToRelayer4 = "FROM /messages/modules/" + loadGenModuleName + "/outputs/pri3 INTO BrokeredEndpoint('/modules/" + relayerModuleName + "/inputs/input1')",
                            }
                        });
                });

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig, token);

            Action<EdgeConfigBuilder> addRelayerConfig = new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(relayerModuleName, relayerImage)
                        .WithEnvironment(new[] { ("receiveOnly", "true") });
        });

            deployment = await this.runtime.DeployConfigurationAsync(addInitialConfig + addRelayerConfig, token);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(trcUrl + "/api/report");
            ITestResultReport[] reports = await response.Content.ReadAsAsync<ITestResultReport[]>();
            Assert.IsTrue(reports[0].IsPassed);

            // Next steps:
            // fill in trc env vars
            // fill in other env vars
            // good to go? - find images of these bad boys and run it
            // Ask damon - Where are these containers run when you run these locally?
        }
    }   
}
