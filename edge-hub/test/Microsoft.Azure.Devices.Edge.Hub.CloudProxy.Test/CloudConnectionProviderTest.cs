// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Moq;

    using Xunit;

    public class CloudConnectionProviderTest
    {
        const int ConnectionPoolSize = 10;
        static readonly IMessageConverterProvider MessageConverterProvider = Mock.Of<IMessageConverterProvider>();
        static readonly ITokenProvider TokenProvider = Mock.Of<ITokenProvider>();
        static readonly IDeviceScopeIdentitiesCache DeviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();

        public static IEnumerable<object[]> UpstreamProtocolTransportSettingsData()
        {
            yield return new object[]
            {
                Option.None<UpstreamProtocol>(),
                20,
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 20,
                            ConnectionIdleTimeout = TimeSpan.FromSeconds(5)
                        }
                    }
                }
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.Amqp),
                30,
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 30,
                            ConnectionIdleTimeout = TimeSpan.FromSeconds(5)
                        }
                    }
                }
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.AmqpWs),
                50,
                new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                        {
                            Pooling = true,
                            MaxPoolSize = 50,
                            ConnectionIdleTimeout = TimeSpan.FromSeconds(5)
                        }
                    }
                }
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.Mqtt),
                60,
                new ITransportSettings[]
                {
                    new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                }
            };

            yield return new object[]
            {
                Option.Some(UpstreamProtocol.MqttWs),
                80,
                new ITransportSettings[]
                {
                    new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only)
                }
            };
        }

        [Fact]
        [Integration]
        public async Task ConnectTest()
        {
            var edgeHub = Mock.Of<IEdgeHub>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(MessageConverterProvider, ConnectionPoolSize, new ClientProvider(), Option.None<UpstreamProtocol>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);
            cloudConnectionProvider.BindEdgeHub(edgeHub);
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            var deviceIdentity = Mock.Of<IDeviceIdentity>(m => m.Id == ConnectionStringHelper.GetDeviceId(deviceConnectionString));
            var clientCredentials = new SharedKeyCredentials(deviceIdentity, deviceConnectionString, null);
            Try<ICloudConnection> cloudProxy = cloudConnectionProvider.Connect(clientCredentials, null).Result;
            Assert.True(cloudProxy.Success);
            bool result = await cloudProxy.Value.CloseAsync();
            Assert.True(result);
        }

        [Fact]
        [Integration]
        public async Task ConnectWithInvalidConnectionStringTest()
        {
            var edgeHub = Mock.Of<IEdgeHub>();
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(MessageConverterProvider, ConnectionPoolSize, new ClientProvider(), Option.None<UpstreamProtocol>(), TokenProvider, DeviceScopeIdentitiesCache, TimeSpan.FromMinutes(60), true);
            cloudConnectionProvider.BindEdgeHub(edgeHub);
            var deviceIdentity1 = Mock.Of<IIdentity>(m => m.Id == "device1");
            var clientCredentials1 = new SharedKeyCredentials(deviceIdentity1, "dummyConnStr", null);
            Try<ICloudConnection> result = await cloudConnectionProvider.Connect(clientCredentials1, null);
            Assert.False(result.Success);
            Assert.IsType<InvalidOperationException>(result.Exception);

            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");

            // Change the connection string key, deliberately.
            char updatedLastChar = (char)(deviceConnectionString[deviceConnectionString.Length - 1] + 1);
            deviceConnectionString = deviceConnectionString.Substring(0, deviceConnectionString.Length - 1) + updatedLastChar;
            var deviceIdentity2 = Mock.Of<IIdentity>(m => m.Id == ConnectionStringHelper.GetDeviceId(deviceConnectionString));
            var clientCredentials2 = new SharedKeyCredentials(deviceIdentity2, deviceConnectionString, null);
            Try<ICloudConnection> cloudProxy = cloudConnectionProvider.Connect(clientCredentials2, null).Result;
            Assert.False(cloudProxy.Success);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(UpstreamProtocolTransportSettingsData))]
        public void GetTransportSettingsTest(Option<UpstreamProtocol> upstreamProtocol, int connectionPoolSize, ITransportSettings[] expectedTransportSettingsList)
        {
            ITransportSettings[] transportSettingsList = CloudConnectionProvider.GetTransportSettings(upstreamProtocol, connectionPoolSize);

            Assert.NotNull(transportSettingsList);
            Assert.Equal(expectedTransportSettingsList.Length, transportSettingsList.Length);
            for (int i = 0; i < expectedTransportSettingsList.Length; i++)
            {
                ITransportSettings expectedTransportSettings = expectedTransportSettingsList[i];
                ITransportSettings transportSettings = transportSettingsList[i];

                Assert.Equal(expectedTransportSettings.GetType(), transportSettings.GetType());
                Assert.Equal(expectedTransportSettings.GetTransportType(), transportSettings.GetTransportType());
                if (expectedTransportSettings is AmqpTransportSettings)
                {
                    Assert.True(((AmqpTransportSettings)expectedTransportSettings).Equals((AmqpTransportSettings)transportSettings));
                }
            }
        }
    }
}
