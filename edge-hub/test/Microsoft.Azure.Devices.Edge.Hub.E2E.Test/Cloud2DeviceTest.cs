// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Message = Microsoft.Azure.Devices.Message;

    [Bvt]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class Cloud2DeviceTest
    {
        ProtocolHeadFixture head = ProtocolHeadFixture.GetInstance();
        const string MessagePropertyName = "property1";
        //use different device than edge device id so the connection is closed between tests
        const string DeviceId = "device2";

        [Fact, TestPriority(101)]
        public async void Receive_C2D_SingleMessage_ShouldSucceed()
        {
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string deviceConnectionString = await this.GetDeviceConnectionString("device2ConnStrKey");

            ServiceClient serviceClient = null;
            DeviceClient deviceClient = null;
            try
            {
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                };
                ITransportSettings[] settings = { mqttSetting };
                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
                await deviceClient.OpenAsync();
                // Dummy ReceiveAsync to ensure mqtt subscription registration before SendAsync() is called on service client.
                await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(2));

                Message message = this.CreateMessage(out string payload);
                await serviceClient.SendAsync(DeviceId, message);

                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);
            }
            finally
            {
                if (deviceClient != null)
                {
                    await deviceClient.CloseAsync();
                }
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }

                // wait for the connection to be closed on the Edge side
                await Task.Delay(TimeSpan.FromSeconds(20));
            }
        }

        [Fact, TestPriority(102)]
        public async void Receive_C2D_OfflineSingleMessage_ShouldSucceed()
        {
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string deviceConnectionString = await this.GetDeviceConnectionString("device2ConnStrKey");

            ServiceClient serviceClient = null;
            DeviceClient deviceClient = null;
            try
            {
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                //Send message before device is listening
                Message message = this.CreateMessage(out string payload);
                await serviceClient.SendAsync(DeviceId, message);

                var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                };
                ITransportSettings[] settings = { mqttSetting };
                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
                await deviceClient.OpenAsync();

                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);
            }
            finally
            {
                if (deviceClient != null)
                {
                    await deviceClient.CloseAsync();
                }
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }

                // wait for the connection to be closed on the Edge side
                await Task.Delay(TimeSpan.FromSeconds(20));
            }
        }

        [Fact, TestPriority(103)]
        public async void Receive_C2D_SingleMessage_AfterOfflineMessage_ShouldSucceed()
        {
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string deviceConnectionString = await this.GetDeviceConnectionString("device2ConnStrKey");

            ServiceClient serviceClient = null;
            DeviceClient deviceClient = null;
            try
            {
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                //Send message before device is listening
                Message message = this.CreateMessage(out string payload);
                await serviceClient.SendAsync(DeviceId, message);

                var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                };
                ITransportSettings[] settings = { mqttSetting };
                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
                await deviceClient.OpenAsync();

                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);

                // send new message after offline was received
                message = this.CreateMessage(out payload);
                await serviceClient.SendAsync(DeviceId, message);
                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);
            }
            finally
            {
                if (deviceClient != null)
                {
                    await deviceClient.CloseAsync();
                }
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }

                // wait for the connection to be closed on the Edge side
                await Task.Delay(TimeSpan.FromSeconds(20));
            }
        }

        Message CreateMessage(out string payload)
        {
            payload = Guid.NewGuid().ToString();
            string messageId = Guid.NewGuid().ToString();
            string property1Value = Guid.NewGuid().ToString();

            var message = new Message(Encoding.UTF8.GetBytes(payload))
            {
                MessageId = messageId,
                Properties =
                {
                    [MessagePropertyName] = property1Value
                }
            };
            return message;
        }

        async Task VerifyReceivedC2DMessage(DeviceClient deviceClient, string payload, string p1Value)
        {
            Client.Message receivedMessage = await deviceClient.ReceiveAsync();

            if (receivedMessage != null)
            {
                string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                Assert.Equal(messageData, payload);
                Assert.Equal(receivedMessage.Properties.Count, 1);
                KeyValuePair<string, string> prop = receivedMessage.Properties.Single();
                Assert.Equal(prop.Key, MessagePropertyName);
                Assert.Equal(prop.Value, p1Value);

                await deviceClient.CompleteAsync(receivedMessage);
            }
            else
            {
                throw new TimeoutException("Test is running longer than expected.");
            }
        }

        async Task<string> GetDeviceConnectionString(string secretConfigKey)
        {
            string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
            string deviceConnectionString = await SecretsHelper.GetSecretFromConfigKey(secretConfigKey);
            return $"{deviceConnectionString};GatewayHostName={gatewayHostname}";
        }
    }
}