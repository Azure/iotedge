// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class CloudConnectionProviderTest
    {
        static readonly Core.IMessageConverterProvider MessageConverterProvider = Mock.Of<IMessageConverterProvider>();
        const int ConnectionPoolSize = 10;

        [Fact]
        [Integration]
        public async Task ConnectTest()
        {
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(MessageConverterProvider, ConnectionPoolSize, new DeviceClientProvider(), Option.None<UpstreamProtocol>());
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            var deviceIdentity = Mock.Of<IIdentity>(m => m.Id == ConnectionStringHelper.GetDeviceId(deviceConnectionString) && m.ConnectionString == deviceConnectionString);
            Try<ICloudConnection> cloudProxy = cloudConnectionProvider.Connect(deviceIdentity, null).Result;
            Assert.True(cloudProxy.Success);
            bool result = await cloudProxy.Value.CloseAsync();
            Assert.True(result);
        }

        [Fact]
        [Integration]
        public async Task ConnectWithInvalidConnectionStringTest()
        {
            ICloudConnectionProvider cloudConnectionProvider = new CloudConnectionProvider(MessageConverterProvider, ConnectionPoolSize, new DeviceClientProvider(), Option.None<UpstreamProtocol>());
            var deviceIdentity1 = Mock.Of<IIdentity>(m => m.Id == "device1" && m.ConnectionString == string.Empty && m.Token == Option.None<string>());
            Try<ICloudConnection> result = await cloudConnectionProvider.Connect(deviceIdentity1, null);
            Assert.False(result.Success);
            Assert.IsType<ArgumentException>(result.Exception);

            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("device1ConnStrKey");
            // Change the connection string key, deliberately.
            char updatedLastChar = (char)(deviceConnectionString[deviceConnectionString.Length - 1] + 1);
            deviceConnectionString = deviceConnectionString.Substring(0, deviceConnectionString.Length - 1) + updatedLastChar;
            var deviceIdentity2 = Mock.Of<IIdentity>(m => m.Id == ConnectionStringHelper.GetDeviceId(deviceConnectionString) && m.ConnectionString == deviceConnectionString);
            Try<ICloudConnection> cloudProxy = cloudConnectionProvider.Connect(deviceIdentity2, null).Result;
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
                           MaxPoolSize = 20
                       }
                   },
                   new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only)
                   {
                       AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                       {
                           Pooling = true,
                           MaxPoolSize = 20
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
                            MaxPoolSize = 30
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
                            MaxPoolSize = 50
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
    }
}
