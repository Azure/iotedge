// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ModuleClientTest
    {
        [Theory]
        [InlineData(UpstreamProtocol.AmqpWs, Client.TransportType.Amqp_WebSocket_Only)]
        [InlineData(UpstreamProtocol.Amqp, Client.TransportType.Amqp_Tcp_Only)]
        [InlineData(UpstreamProtocol.MqttWs, Client.TransportType.Mqtt_WebSocket_Only)]
        [InlineData(UpstreamProtocol.Mqtt, Client.TransportType.Mqtt_Tcp_Only)]
        [Unit]
        public async Task CreateForUpstreamProtocolTest(UpstreamProtocol upstreamProtocol, Client.TransportType expectedTransportType)
        {
            // Arrange
            var receivedTransportType = Client.TransportType.Http1;
            Task<Client.ModuleClient> ModuleClientCreator(Client.TransportType transportType)
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
            var receivedTransportTypes = new List<Client.TransportType>();
            Task<Client.ModuleClient> DeviceClientCreator(Client.TransportType transportType)
            {
                receivedTransportTypes.Add(transportType);
                if (receivedTransportTypes.Count == 1)
                {
                    throw new InvalidOperationException();
                }
                return Task.FromResult((Client.ModuleClient)null);
            }

            // Act
            await ModuleClient.CreateDeviceClientForUpstreamProtocol(Option.None<UpstreamProtocol>(), DeviceClientCreator);

            // Assert
            Assert.Equal(2, receivedTransportTypes.Count);
            Assert.Equal(Client.TransportType.Amqp_Tcp_Only, receivedTransportTypes[0]);
            Assert.Equal(Client.TransportType.Amqp_WebSocket_Only, receivedTransportTypes[1]);
        }
    }
}
