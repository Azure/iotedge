// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using ModuleClient = Microsoft.Azure.Devices.Edge.Agent.IoTHub.ModuleClient;

    public class ModuleClientTest
    {
        [Theory]
        [InlineData(UpstreamProtocol.AmqpWs)]
        [InlineData(UpstreamProtocol.Amqp)]
        [InlineData(UpstreamProtocol.MqttWs)]
        [InlineData(UpstreamProtocol.Mqtt)]
        [Unit]
        public async Task CreateForUpstreamProtocolTest(UpstreamProtocol upstreamProtocol)
        {
            // Arrange
            Option<UpstreamProtocol> receivedProtocol = Option.None<UpstreamProtocol>();
            Task<Client.ModuleClient> ModuleClientCreator(UpstreamProtocol up)
            {
                receivedProtocol = Option.Some(up);
                return Task.FromResult((Client.ModuleClient)null);
            }

            // Act
            await ModuleClient.CreateDeviceClientForUpstreamProtocol(Option.Some(upstreamProtocol), ModuleClientCreator);

            // Assert
            Assert.Equal(Option.Some(upstreamProtocol), receivedProtocol);
        }

        [Unit]
        [Fact]
        public async Task CreateForNoUpstreamProtocolTest()
        {
            // Arrange
            var receivedProtocols = new List<UpstreamProtocol>();
            Task<Client.ModuleClient> DeviceClientCreator(UpstreamProtocol up)
            {
                receivedProtocols.Add(up);
                return receivedProtocols.Count == 1
                    ? Task.FromException<Client.ModuleClient>(new InvalidOperationException())
                    : Task.FromResult(
                        Client.ModuleClient.Create(
                            "example.com",
                            new ModuleAuthenticationWithToken("deviceid", "moduleid", TokenHelper.CreateSasToken("foo.azure-devices.net"))));
            }

            // Act
            await ModuleClient.CreateDeviceClientForUpstreamProtocol(Option.None<UpstreamProtocol>(), DeviceClientCreator);

            // Assert
            Assert.Equal(2, receivedProtocols.Count);
            Assert.Equal(UpstreamProtocol.Amqp, receivedProtocols[0]);
            Assert.Equal(UpstreamProtocol.AmqpWs, receivedProtocols[1]);
        }
    }
}
