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
        [Category("CentOsSafe")]
        // This test should be disabled on windows until the following is resolved:
        // https://github.com/Azure/azure-iot-sdk-csharp/issues/2223
        [Category("FlakyOnWindows")]
        public async Task TempSensor()
        {
            string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(SensorName, sensorImage)
                        .WithEnvironment(new[] { ("MessageCount", "1"), ("StartDelay", "00:00:30") });
                },
                token);

            EdgeModule sensor = deployment.Modules[SensorName];
            await sensor.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [Test]
        [Category("CentOsSafe")]
        [Category("Flaky")]
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
                token);

            EdgeModule filter = deployment.Modules[filterName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [Test]
        [Category("Flaky")]
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
                token);

            EdgeModule filter = deployment.Modules[filterFuncName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [Test]
        [Category("CentOsSafe")]
        [Category("Flaky")]
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

        [Test]
        [Category("CentOsSafe")]
        public async Task MetricsCollector()
        {
            const string metricsCollectorName = "metricsCollector";

            string metricsCollectorImage = Context.Current.MetricsCollectorImage.Expect(() => new ArgumentException("metricsCollectorImage parameter is required for MetricsCollector test"));
            string hubResourceId = Context.Current.HubResourceId.Expect(() => new ArgumentException("IOT_HUB_RESOURCE_ID is required for MetricsCollector test"));

            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule(metricsCollectorName, metricsCollectorImage)
                        .WithEnvironment(new[]
                        {
                            ("UploadTarget", "IotMessage"),
                            ("ResourceID", hubResourceId)
                        });
                    builder.GetModule(ModuleName.EdgeHub)
                        .WithDesiredProperties(new Dictionary<string, object>
                        {
                            ["routes"] = new
                            {
                                AzureIotEdgeMetricsCollectorToCloud = $"FROM /messages/modules/{metricsCollectorName}/* INTO $upstream"
                            }
                        });
                },
                token);

            EdgeModule azureIotEdgeMetricsCollector = deployment.Modules[metricsCollectorName];
            await azureIotEdgeMetricsCollector.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }
    }
}
