// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using NUnit.Framework;

    public class Module : SasManualProvisioningFixture
    {
        private sealed class TempSensorModule
        {
            public string Name { get; }
            public string Image { get; }

            private const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";
            private static int instanceCount = 0;

            private TempSensorModule(int number)
            {
                this.Name = "tempSensor" + number.ToString();
                this.Image = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
            }

            public static TempSensorModule GetInstance()
            {
                return new TempSensorModule(TempSensorModule.instanceCount++);
            }
        }

        [Test]
        public async Task TempSensor()
        {
            var tempSensorModule = TempSensorModule.GetInstance();
            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(tempSensorModule.Name, tempSensorModule.Image)
                        .WithEnvironment(new[] { ("MessageCount", "1") });
                },
                token);

            EdgeModule sensor = deployment.Modules[tempSensorModule.Name];
            await sensor.WaitForEventsReceivedAsync(deployment.StartTime, token);

            await sensor.UpdateDesiredPropertiesAsync(
                new
                {
                    properties = new
                    {
                        desired = new
                        {
                            SendData = true,
                            SendInterval = 10
                        }
                    }
                },
                token);
            await sensor.WaitForReportedPropertyUpdatesAsync(
                new
                {
                    properties = new
                    {
                        reported = new
                        {
                            SendData = true,
                            SendInterval = 10
                        }
                    }
                },
                token);
        }

        [Test]
        public async Task TempFilter()
        {
            string filterImage = Context.Current.TempFilterImage.Expect(() => new ArgumentException("tempFilterImage parameter is required for TempFilter test"));

            const string filterModuleName = "tempFilter";
            var tempSensorModule = TempSensorModule.GetInstance();

            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(tempSensorModule.Name, tempSensorModule.Image)
                        .WithEnvironment(new[] { ("MessageCount", "1") });
                    builder.AddModule(filterModuleName, filterImage)
                        .WithEnvironment(new[] { ("TemperatureThreshold", "19") });
                    builder.GetModule("$edgeHub")
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new
                            {
                                TempFilterToCloud = "FROM /messages/modules/" + filterModuleName + "/outputs/alertOutput INTO $upstream",
                                TempSensorToTempFilter = "FROM /messages/modules/" + tempSensorModule.Name + "/outputs/temperatureOutput INTO BrokeredEndpoint('/modules/" + filterModuleName + "/inputs/input1')"
                            }
                        } );
                },
                token);

            EdgeModule filter = deployment.Modules[filterModuleName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [Test]
        // Test Temperature Filter Function: https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-deploy-function
        public async Task TempFilterFunc()
        {
            // Azure Fucntion Name: EdgeHubTrigger-CSharp
            string filterFunc = Context.Current.TempFilterFuncImage.Expect(() => new ArgumentException("'tempFilterFuncImage' parameter is required for TempFilterFunc() test"));

            const string filterFuncModuleName = "tempFilterFunctions";
            var tempSensorModule = TempSensorModule.GetInstance();

            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(tempSensorModule.Name, tempSensorModule.Image)
                        .WithEnvironment(new[] { ("MessageCount", "1") });
                    builder.AddModule(filterFuncModuleName, filterFunc)
                        .WithEnvironment(new[] { ("AZURE_FUNCTIONS_ENVIRONMENT", "Development") });
                    builder.GetModule("$edgeHub")
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new
                            {
                                TempFilterFunctionsToCloud = "FROM /messages/modules/" + filterFuncModuleName + "/outputs/output1 INTO $upstream",
                                TempSensorToTempFilter = "FROM /messages/modules/" + tempSensorModule.Name + "/outputs/temperatureOutput " +
                                                            "INTO BrokeredEndpoint('/modules/" + filterFuncModuleName + "/inputs/input1')"
                            }
                        });
                },
                token);

            EdgeModule filter = deployment.Modules[filterFuncModuleName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [Test]
        public async Task ModuleToModuleDirectMethod(
            [Values] Protocol protocol)
        {
            if (OsPlatform.IsWindows() && (protocol == Protocol.AmqpWs || protocol == Protocol.MqttWs))
            {
                Assert.Ignore("Module-to-module direct methods don't work over WebSocket on Windows");
            }

            string senderImage = Context.Current.MethodSenderImage.Expect(() => new InvalidOperationException("Missing Direct Method Sender image"));
            string receiverImage = Context.Current.MethodReceiverImage.Expect(() => new InvalidOperationException("Missing Direct Method Receiver image"));
            string methodSender = $"methodSender-{protocol.ToString()}";
            string methodReceiver = $"methodReceiver-{protocol.ToString()}";

            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    string clientTransport = protocol.ToTransportType().ToString();

                    builder.AddModule(methodSender, senderImage)
                        .WithEnvironment(
                            new[]
                            {
                                ("ClientTransportType", clientTransport),
                                ("TargetModuleId", methodReceiver)
                            });
                    builder.AddModule(methodReceiver, receiverImage)
                        .WithEnvironment(new[] { ("ClientTransportType", clientTransport) });
                },
                token);

            EdgeModule sender = deployment.Modules[methodSender];
            await sender.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }
    }
}
