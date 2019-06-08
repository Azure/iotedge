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
            string edgeAgent = Context.Current.EdgeAgent.Expect(() => new ArgumentException());
            string edgeHub = Context.Current.EdgeHub.Expect(() => new ArgumentException());
            string tempSensor = Context.Current.TempSensor.Expect(() => new ArgumentException());
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

                    var builder = new EdgeConfigBuilder(device.Id, iotHub);
                    Context.Current.Registry.ForEach(
                        r => builder.AddRegistryCredentials(r.address, r.username, r.password));
                    builder.AddEdgeAgent(edgeAgent).WithProxy(proxy, Protocol.Amqp);
                    builder.AddEdgeHub(edgeHub).WithProxy(proxy, Protocol.Amqp);
                    builder.AddModule("tempSensor", tempSensor);
                    await builder.Build().DeployAsync(token);

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
            [Values("Amqp", "Mqtt")] string edgeToCloud,
            [Values("Amqp", "Mqtt")] string moduleToEdge)
        {
            string edgeAgent = Context.Current.EdgeAgent.Expect(() => new ArgumentException());
            string edgeHub = Context.Current.EdgeHub.Expect(() => new ArgumentException());
            string senderImage = Context.Current.MethodSender.Expect(() => new ArgumentException());
            string receiverImage = Context.Current.MethodReceiver.Expect(() => new ArgumentException());
            Option<Uri> proxy = Context.Current.Proxy;

            CancellationToken token = this.cts.Token;

            string name = $"module-to-module direct method (upstream:{edgeToCloud}, modules:{moduleToEdge})";
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

                    string methodSender = $"methodSender-{edgeToCloud}-{moduleToEdge}";
                    string methodReceiver = $"methodReceiver-{edgeToCloud}-{moduleToEdge}";

                    var builder = new EdgeConfigBuilder(device.Id, iotHub);
                    Context.Current.Registry.ForEach(
                        r => builder.AddRegistryCredentials(r.address, r.username, r.password));
                    builder.AddEdgeAgent(edgeAgent)
                        .WithEnvironment(new[] { ("UpstreamProtocol", edgeToCloud) })
                        .WithProxy(proxy, Enum.Parse<Protocol>(edgeToCloud));
                    builder.AddEdgeHub(edgeHub)
                        .WithEnvironment(new[] { ("UpstreamProtocol", edgeToCloud) })
                        .WithProxy(proxy, Enum.Parse<Protocol>(edgeToCloud));
                    builder.AddModule(methodSender, senderImage)
                        .WithEnvironment(
                            new[]
                            {
                                ("UpstreamProtocol", moduleToEdge),
                                ("TargetModuleId", methodReceiver)
                            });
                    builder.AddModule(methodReceiver, receiverImage)
                        .WithEnvironment(new[] { ("UpstreamProtocol", moduleToEdge) });
                    await builder.Build().DeployAsync(token);

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
