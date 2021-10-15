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
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

    [EndToEnd]
    public class Module : SasManualProvisioningFixture
    {
        const string SensorName = "tempSensor";
        const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";

        [Test]
        [Category("ManifestSigning")]
        [Category("CentOsSafe")]
        [Category("nestededge_isa95")]
        public async Task TempSensor()
        {
            string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
            CancellationToken token = this.TestToken;

            EdgeModule sensor;
            DateTime startTime;

            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                    builder =>
                    {
                        builder.AddModule(SensorName, sensorImage)
                            .WithEnvironment(new[] { ("MessageCount", "-1") });
                    },
                    token,
                    Context.Current.NestedEdge);
                sensor = deployment.Modules[SensorName];
                startTime = deployment.StartTime;
            }
            else
            {
                sensor = new EdgeModule(SensorName, this.runtime.DeviceId, this.IotHub);
                startTime = DateTime.Now;
            }

            await sensor.WaitForEventsReceivedAsync(startTime, token);

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
        [Category("CentOsSafe")]
        public async Task TempFilter()
        {
            const string filterName = "tempFilter";

            string filterImage = Context.Current.TempFilterImage.Expect(
                () => new ArgumentException("tempFilterImage parameter is required for TempFilter test"));
            string sensorImage = Context.Current.TempSensorImage.Expect(
                () => new ArgumentException("tempSensorImage parameter is required for TempFilter test"));

            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(SensorName, sensorImage)
                        .WithEnvironment(new[] { ("MessageCount", "1") });
                    builder.AddModule(filterName, filterImage)
                        .WithEnvironment(new[] { ("TemperatureThreshold", "19") });
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new
                            {
                                TempFilterToCloud = $"FROM /messages/modules/{filterName}/outputs/alertOutput INTO $upstream",
                                TempSensorToTempFilter = $"FROM /messages/modules/{SensorName}/outputs/temperatureOutput INTO BrokeredEndpoint('/modules/{filterName}/inputs/input1')"
                            }
                        });
                },
                token,
                Context.Current.NestedEdge);

            EdgeModule filter = deployment.Modules[filterName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [Test]
        [Category("FlakyOnArm")]
        [Category("CentOsSafe")]
        // Test Temperature Filter Function: https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-deploy-function
        public async Task TempFilterFunc()
        {
            const string filterFuncName = "tempFilterFunctions";

            // Azure Function Name: EdgeHubTrigger-CSharp
            string filterFuncImage = Context.Current.TempFilterFuncImage.Expect(
                () => new ArgumentException("tempFilterFuncImage parameter is required for TempFilterFunc test"));
            string sensorImage = Context.Current.TempSensorImage.Expect(
                () => new ArgumentException("tempSensorImage parameter is required for TempFilterFunc test"));

            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(SensorName, sensorImage)
                        .WithEnvironment(new[] { ("MessageCount", "1") });
                    builder.AddModule(filterFuncName, filterFuncImage)
                        .WithEnvironment(new[] { ("AZURE_FUNCTIONS_ENVIRONMENT", "Development") });
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new
                            {
                                TempFilterFunctionsToCloud = $"FROM /messages/modules/{filterFuncName}/outputs/output1 INTO $upstream",
                                TempSensorToTempFilter = $"FROM /messages/modules/{SensorName}/outputs/temperatureOutput INTO BrokeredEndpoint('/modules/{filterFuncName}/inputs/input1')"
                            }
                        });
                },
                token,
                Context.Current.NestedEdge);

            EdgeModule filter = deployment.Modules[filterFuncName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [Test]
        [Category("CentOsSafe")]
        public async Task ModuleToModuleDirectMethod(
            [Values] Protocol protocol)
        {
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
                                ("TransportType", clientTransport),
                                ("TargetModuleId", methodReceiver)
                            });
                    builder.AddModule(methodReceiver, receiverImage)
                        .WithEnvironment(new[] { ("ClientTransportType", clientTransport) });
                },
                token,
                Context.Current.NestedEdge);

            EdgeModule sender = deployment.Modules[methodSender];
            await sender.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }
    }
}
