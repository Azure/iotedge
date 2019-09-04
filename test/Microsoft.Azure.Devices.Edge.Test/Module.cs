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

    public class Module : RuntimeFixture
    {
        [Test]
        public async Task TempSensor()
        {
            const string DefaultImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";
            string sensorImage = Context.Current.TempSensorImage.GetOrElse(DefaultImage);

            CancellationToken token = this.cts.Token;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder => { builder.AddModule("tempSensor", sensorImage); },
                token);

            EdgeModule sensor = deployment.Modules["tempSensor"];
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
