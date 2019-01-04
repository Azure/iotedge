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
        [InlineData(UpstreamProtocol.AmqpWs, TransportType.Amqp_WebSocket_Only)]
        [InlineData(UpstreamProtocol.Amqp, TransportType.Amqp_Tcp_Only)]
        [InlineData(UpstreamProtocol.MqttWs, TransportType.Mqtt_WebSocket_Only)]
        [InlineData(UpstreamProtocol.Mqtt, TransportType.Mqtt_Tcp_Only)]
        [Unit]
        public async Task CreateForUpstreamProtocolTest(UpstreamProtocol upstreamProtocol, TransportType expectedTransportType)
        {
            // Arrange
            var receivedTransportType = TransportType.Http1;

            Task<Client.ModuleClient> ModuleClientCreator(TransportType transportType)
            {
                receivedTransportType = transportType;
                return Task.FromResult((Client.ModuleClient)null);
            }

            // Act
            await ModuleClient.CreateDeviceClientForUpstreamProtocol(Option.Some(upstreamProtocol), ModuleClientCreator);

            // Assert
            Assert.Equal(expectedTransportType, receivedTransportType);
        }

        [Unit]
        [Fact]
        public async Task CreateForNoUpstreamProtocolTest()
        {
            // Arrange
            var receivedTransportTypes = new List<TransportType>();

            Task<Client.ModuleClient> DeviceClientCreator(TransportType transportType)
            {
                receivedTransportTypes.Add(transportType);
                return receivedTransportTypes.Count == 1
                    ? Task.FromException<Client.ModuleClient>(new InvalidOperationException())
                    : Task.FromResult(
                        Client.ModuleClient.Create(
                            "example.com",
                            new ModuleAuthenticationWithToken("deviceid", "moduleid", TokenHelper.CreateSasToken("foo.azure-devices.net"))));
            }

            // Act
            await ModuleClient.CreateDeviceClientForUpstreamProtocol(Option.None<UpstreamProtocol>(), DeviceClientCreator);

            // Assert
            Assert.Equal(2, receivedTransportTypes.Count);
            Assert.Equal(TransportType.Amqp_Tcp_Only, receivedTransportTypes[0]);
            Assert.Equal(TransportType.Amqp_WebSocket_Only, receivedTransportTypes[1]);
        }
    }
}
