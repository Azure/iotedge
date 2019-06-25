// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;
    using Serilog;

    public class EndToEnd
    {
        CancellationTokenSource cts;

        [SetUp]
        public void Setup() => this.cts = new CancellationTokenSource(Context.Current.TestTimeout);

        [TearDown]
        public void Teardown() => this.cts.Dispose();

        [Test]
        public async Task TempSensor()
        {
            string agentImage = Context.Current.EdgeAgentImage.Expect(() => new ArgumentException());
            string hubImage = Context.Current.EdgeHubImage.Expect(() => new ArgumentException());
            string sensorImage = Context.Current.TempSensorImage.Expect(() => new ArgumentException());
            bool optimizeForPerformance = Context.Current.OptimizeForPerformance;
            Option<Uri> proxy = Context.Current.Proxy;

            CancellationToken token = this.cts.Token;

            string name = "temp sensor";
            Log.Information("Running test '{Name}'", name);
            await Profiler.Run(
                async () =>
                {
                    var iotHub = new IotHub(
                        Context.Current.ConnectionString,
                        Context.Current.EventHubEndpoint,
                        proxy);

                    EdgeDevice device = (await EdgeDevice.GetIdentityAsync(
                        Context.Current.DeviceId,
                        iotHub,
                        token)).Expect(() => new Exception("Device should have already been created in setup fixture"));

                    var builder = new EdgeConfigBuilder(device.Id);
                    Context.Current.Registry.ForEach(
                        r => builder.AddRegistryCredentials(r.address, r.username, r.password));
                    builder.AddEdgeAgent(agentImage).WithProxy(proxy);
                    builder.AddEdgeHub(hubImage, optimizeForPerformance).WithProxy(proxy);
                    builder.AddModule("tempSensor", sensorImage);
                    await builder.Build().DeployAsync(iotHub, token);

                    var hub = new EdgeModule("edgeHub", device.Id, iotHub);
                    var sensor = new EdgeModule("tempSensor", device.Id, iotHub);
                    await EdgeModule.WaitForStatusAsync(
                        new[] { hub, sensor },
                        EdgeModuleStatus.Running,
                        token);
                    await sensor.WaitForEventsReceivedAsync(token);

                    var sensorTwin = new ModuleTwin(sensor.Id, device.Id, iotHub);
                    await sensorTwin.UpdateDesiredPropertiesAsync(
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
                    await sensorTwin.WaitForReportedPropertyUpdatesAsync(
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
                },
                "Completed test '{Name}'",
                name);
        }

        [Test]
        public async Task ModuleToModuleDirectMethod(
            [Values] Protocol protocol)
        {
            if (Platform.IsWindows() && (protocol == Protocol.AmqpWs || protocol == Protocol.MqttWs))
            {
                Assert.Ignore("Module-to-module direct methods don't work over WebSocket on Windows");
            }

            string agentImage = Context.Current.EdgeAgentImage.Expect(() => new ArgumentException());
            string hubImage = Context.Current.EdgeHubImage.Expect(() => new ArgumentException());
            string senderImage = Context.Current.MethodSenderImage.Expect(() => new ArgumentException());
            string receiverImage = Context.Current.MethodReceiverImage.Expect(() => new ArgumentException());
            bool optimizeForPerformance = Context.Current.OptimizeForPerformance;
            Option<Uri> proxy = Context.Current.Proxy;

            CancellationToken token = this.cts.Token;

            string name = $"module-to-module direct method ({protocol.ToString()})";
            Log.Information("Running test '{Name}'", name);
            await Profiler.Run(
                async () =>
                {
                    var iotHub = new IotHub(
                        Context.Current.ConnectionString,
                        Context.Current.EventHubEndpoint,
                        proxy);

                    EdgeDevice device = (await EdgeDevice.GetIdentityAsync(
                        Context.Current.DeviceId,
                        iotHub,
                        token)).Expect(() => new Exception("Device should have already been created in setup fixture"));

                    string methodSender = $"methodSender-{protocol.ToString()}";
                    string methodReceiver = $"methodReceiver-{protocol.ToString()}";
                    string clientTransport = protocol.ToTransportType().ToString();

                    var builder = new EdgeConfigBuilder(device.Id);
                    Context.Current.Registry.ForEach(
                        r => builder.AddRegistryCredentials(r.address, r.username, r.password));
                    builder.AddEdgeAgent(agentImage).WithProxy(proxy);
                    builder.AddEdgeHub(hubImage, optimizeForPerformance).WithProxy(proxy);
                    builder.AddModule(methodSender, senderImage)
                        .WithEnvironment(
                            new[]
                            {
                                ("ClientTransportType", clientTransport),
                                ("TargetModuleId", methodReceiver)
                            });
                    builder.AddModule(methodReceiver, receiverImage)
                        .WithEnvironment(new[] { ("ClientTransportType", clientTransport) });
                    await builder.Build().DeployAsync(iotHub, token);

                    var hub = new EdgeModule("edgeHub", device.Id, iotHub);
                    var sender = new EdgeModule(methodSender, device.Id, iotHub);
                    var receiver = new EdgeModule(methodReceiver, device.Id, iotHub);
                    await EdgeModule.WaitForStatusAsync(
                        new[] { hub, sender, receiver },
                        EdgeModuleStatus.Running,
                        token);
                    await sender.WaitForEventsReceivedAsync(token);
                },
                "Completed test '{Name}'",
                name);
        }
    }
}
