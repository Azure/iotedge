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

    [TestClass]
    [TestCategory("EndToEnd")]
    public class Module : SasManualProvisioningFixture
    {
        const string SensorName = "tempSensor";
        const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.5";

        [TestMethod]
        [DataRow(Protocol.Mqtt)]
        [DataRow(Protocol.Amqp)]
        public async Task CertRenew(Protocol protocol)
        {
            this.TestContext.Properties.Add("Row", protocol.ToString());
            CancellationToken token = TestToken;

            EdgeDeployment deployment = await runtime.DeployConfigurationAsync(
                    builder =>
                    {
                        builder.GetModule(ModuleName.EdgeHub).WithEnvironment(("ServerCertificateRenewAfterInMs", "6000"));
                        builder.GetModule(ModuleName.EdgeHub).WithEnvironment(new[] { ("UpstreamProtocol", protocol.ToString()) });
                    },
                    cli,
                    token,
                    Context.Current.NestedEdge);

            EdgeModule edgeHub = deployment.Modules[ModuleName.EdgeHub];
            await edgeHub.WaitForStatusAsync(EdgeModuleStatus.Running, cli, token);
            EdgeModule edgeAgent = deployment.Modules[ModuleName.EdgeAgent];
            // certificate renew should stop edgeHub and then it should be started by edgeAgent
            await edgeAgent.WaitForReportedPropertyUpdatesAsync(
                new
                {
                    properties = new
                    {
                        reported = new
                        {
                            systemModules = new
                            {
                                edgeHub = new
                                {
                                    restartCount = 1
                                }
                            }
                        }
                    }
                },
                token);
        }

        [TestMethod]
        [TestCategory("nestededge_isa95")]
        public async Task TempSensor()
        {
            string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultSensorImage);
            CancellationToken token = TestToken;

            EdgeModule sensor;
            DateTime startTime;

            // This is a temporary solution see ticket: 9288683
            if (!Context.Current.ISA95Tag)
            {
                EdgeDeployment deployment = await runtime.DeployConfigurationAsync(
                    builder =>
                    {
                        builder.AddModule(SensorName, sensorImage)
                            .WithEnvironment(new[] { ("MessageCount", "-1") });
                    },
                    cli,
                    token,
                    Context.Current.NestedEdge);
                sensor = deployment.Modules[SensorName];
                startTime = deployment.StartTime;
            }
            else
            {
                sensor = new EdgeModule(SensorName, runtime.DeviceId, IotHub);
                startTime = DateTime.Now;
            }

            await sensor.WaitForEventsReceivedAsync(startTime, token);
        }

        [TestMethod]
        public async Task TempFilter()
        {
            const string filterName = "tempFilter";

            string filterImage = Context.Current.TempFilterImage.Expect(
                () => new ArgumentException("tempFilterImage parameter is required for TempFilter test"));
            string sensorImage = Context.Current.TempSensorImage.Expect(
                () => new ArgumentException("tempSensorImage parameter is required for TempFilter test"));

            CancellationToken token = TestToken;

            EdgeDeployment deployment = await runtime.DeployConfigurationAsync(
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
                cli,
                token,
                Context.Current.NestedEdge);

            EdgeModule filter = deployment.Modules[filterName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [TestMethod]
        [TestCategory("Amd64Only")]
        // Test Temperature Filter Function: https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-deploy-function
        public async Task TempFilterFunc()
        {
            const string filterFuncName = "tempFilterFunctions";

            // Azure Function Name: EdgeHubTrigger-CSharp
            string filterFuncImage = Context.Current.TempFilterFuncImage.Expect(
                () => new ArgumentException("tempFilterFuncImage parameter is required for TempFilterFunc test"));
            string sensorImage = Context.Current.TempSensorImage.Expect(
                () => new ArgumentException("tempSensorImage parameter is required for TempFilterFunc test"));

            CancellationToken token = TestToken;

            EdgeDeployment deployment = await runtime.DeployConfigurationAsync(
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
                cli,
                token,
                Context.Current.NestedEdge);

            EdgeModule filter = deployment.Modules[filterFuncName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }

        [TestMethod]
        [DataRow(Protocol.Amqp)]
        [DataRow(Protocol.AmqpWs)]
        [DataRow(Protocol.Mqtt)]
        [DataRow(Protocol.MqttWs)]
        public async Task ModuleToModuleDirectMethod(Protocol protocol)
        {
            this.TestContext.Properties.Add("Row", $"{protocol}");
            string senderImage = Context.Current.MethodSenderImage.Expect(() => new InvalidOperationException("Missing Direct Method Sender image"));
            string receiverImage = Context.Current.MethodReceiverImage.Expect(() => new InvalidOperationException("Missing Direct Method Receiver image"));
            string methodSender = $"methodSender-{protocol.ToString()}";
            string methodReceiver = $"methodReceiver-{protocol.ToString()}";

            CancellationToken token = TestToken;

            EdgeDeployment deployment = await runtime.DeployConfigurationAsync(
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
                cli,
                token,
                Context.Current.NestedEdge);

            EdgeModule sender = deployment.Modules[methodSender];
            await sender.WaitForEventsReceivedAsync(deployment.StartTime, token);
        }
    }
}
