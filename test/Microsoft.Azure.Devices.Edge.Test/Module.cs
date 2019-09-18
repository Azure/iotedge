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

    public class Module : RuntimeFixture
    {
        private string GetTempSensorImage()
        {
            const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";
            return Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
        }

        [Category("TempSensor")]
        [Test]
        public async Task TempSensor()
        {
            const string tempSensorModName = "tempSensor";
            CancellationToken token = this.cts.Token;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder => { builder.AddModule(tempSensorModName, this.GetTempSensorImage()); },
                token);

            EdgeModule sensor = deployment.Modules[tempSensorModName];
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

        [Category("TempSensor")]
        [Test]
        public async Task TempFilter()
        {
            string filterImage = Context.Current.TempFilterImage.Expect(() => new ArgumentException("tempFilterImage parameter is required for TempFilter test"));

            const string filterModName = "tempFilter";
            const string tempSensorModName = "tempSensor";

            CancellationToken token = this.cts.Token;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                             builder.AddModule(tempSensorModName, this.GetTempSensorImage());
                             builder.AddModule(filterModName, filterImage)
                                    .WithEnvironment(new[] { ("TemperatureThreshold", "19") });
                             builder.GetModule("$edgeHub")
                                    .WithDesiredProperties(new Dictionary<string, object>
                                    {
                                        ["routes"] = new
                                        {
                                            TempFilterToCloud = "FROM /messages/modules/" + filterModName + "/outputs/alertOutput INTO $upstream",
                                            TempSensorToTempFilter = "FROM /messages/modules/" + tempSensorModName + "/outputs/temperatureOutput INTO BrokeredEndpoint('/modules/" + filterModName + "/inputs/input1')"
                                        }
                                    } );
                },
                token);

            EdgeModule filter = deployment.Modules[filterModName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [Category("TempSensor")]
        [Test]
        // Test Temperature Filter Function: https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-deploy-function
        public async Task TempFilterFunc()
        {
            string filterFunc = Context.Current.TempFilterFunc.Expect(() => new ArgumentException("'tempFilterFunc' parameter is required for TempFilterFunc() test"));

            const string filterFuncModuleName = "tempFilterFunctions";
            const string tempSensorModName = "tempSensor";

            CancellationToken token = this.cts.Token;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                             builder.AddModule(tempSensorModName, this.GetTempSensorImage());
                             builder.AddModule(filterFuncModuleName, filterFunc)
                                    .WithEnvironment(new[] { ("AZURE_FUNCTIONS_ENVIRONMENT", "Development") });
                             builder.GetModule("$edgeHub")
                                    .WithDesiredProperties(new Dictionary<string, object>
                                    {
                                        ["routes"] = new
                                        {
                                            TempFilterFunctionsToCloud = "FROM /messages/modules/" + filterFuncModuleName + "/outputs/alertOutput INTO $upstream",
                                            TempSensorToTempFilter = "FROM /messages/modules/" + tempSensorModName + "/outputs/temperatureOutput " +
                                                                     "INTO BrokeredEndpoint('/modules/" + filterFuncModuleName + "/inputs/input1')"
                                        }
                                    } );
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

            string senderImage = Context.Current.MethodSenderImage.Expect(() => new ArgumentException());
            string receiverImage = Context.Current.MethodReceiverImage.Expect(() => new ArgumentException());
            string methodSender = $"methodSender-{protocol.ToString()}";
            string methodReceiver = $"methodReceiver-{protocol.ToString()}";

            CancellationToken token = this.cts.Token;

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
