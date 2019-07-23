// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using NUnit.Framework;

    public class Module : ModuleBase
    {
        [Test]
        public async Task TempSensor()
        {
            string sensorImage = Context.Current.TempSensorImage.Expect(() => new ArgumentException());
            string deviceId = Context.Current.DeviceId;

            CancellationToken token = this.cts.Token;

            DateTime seekTime = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.AddModule("tempSensor", sensorImage);
                },
                token);

            var sensor = new EdgeModule("tempSensor", deviceId);
            await this.runtime.WaitForModulesRunningAsync(new[] { sensor }, token);

            await sensor.WaitForEventsReceivedAsync(seekTime, this.iotHub, token);

            var sensorTwin = new ModuleTwin(sensor.Id, deviceId, this.iotHub);
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
            string deviceId = Context.Current.DeviceId;

            CancellationToken token = this.cts.Token;

            DateTime seekTime = await this.runtime.DeployConfigurationAsync(
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

            var sender = new EdgeModule(methodSender, deviceId);
            var receiver = new EdgeModule(methodReceiver, deviceId);
            await this.runtime.WaitForModulesRunningAsync(new[] { sender, receiver }, token);

            await sender.WaitForEventsReceivedAsync(seekTime, this.iotHub, token);
        }
    }
}
