// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using JetBrains.Annotations;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Message = Microsoft.Azure.Devices.Message;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class Cloud2DeviceTest
    {
        const string MessagePropertyName = "property1";
        const string DeviceNamePrefix = "E2E_c2d_";
        static readonly TimeSpan ClockSkewAdjustment = TimeSpan.FromSeconds(35);

        [Theory]
        [TestPriority(101)]
        [InlineData(TransportType.Mqtt_Tcp_Only)]
        // [InlineData(TransportType.Mqtt_WebSocket_Only)] // Disabled: need a valid server cert for WebSocket to work
        public async void Receive_C2D_SingleMessage_ShouldSucceed(TransportType transportType)
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string edgeDeviceId = ConnectionStringHelper.GetDeviceId(ConfigHelper.TestConfig[Service.Constants.ConfigKey.IotHubConnectionString]);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            var edgeDevice = await rm.GetDeviceAsync(edgeDeviceId);
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm, scope: edgeDevice.Scope);

            ServiceClient serviceClient = null;
            DeviceClient deviceClient = null;
            try
            {
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                ITransportSettings[] settings = this.GetTransportSettings(transportType);
                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
                // Dummy ReceiveAsync to ensure mqtt subscription registration before SendAsync() is called on service client.
                await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(2));

                // Act
                Message message = this.CreateMessage(out string payload);
                await serviceClient.SendAsync(deviceName, message);

                // Assert
                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);
            }
            finally
            {
                await this.Cleanup(deviceClient, serviceClient, rm, deviceName);
            }
        }

        [Fact]
        [TestPriority(102)]
        public async void Receive_C2D_SingleMessage_AfterOfflineMessage_ShouldSucceed()
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string edgeDeviceId = ConnectionStringHelper.GetDeviceId(ConfigHelper.TestConfig[Service.Constants.ConfigKey.IotHubConnectionString]);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            var edgeDevice = await rm.GetDeviceAsync(edgeDeviceId);

            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm, scope: edgeDevice.Scope);

            ServiceClient serviceClient = null;
            DeviceClient deviceClient = null;
            try
            {
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                ITransportSettings[] settings = this.GetTransportSettings();
                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
                // Dummy ReceiveAsync to ensure mqtt subscription registration before SendAsync() is called on service client.
                await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(1));

                var device = await rm.GetDeviceAsync(deviceName);
                // Wait for device to be connected to cloud
                await this.WaitForDeviceConnectionStateTimeoutAfter(rm, deviceName, DeviceConnectionState.Connected, TimeSpan.FromSeconds(60));
                await deviceClient.CloseAsync();

                // Wait for the connection to be closed on the Edge side by checking device connection state
                await this.WaitForDeviceConnectionStateTimeoutAfter(rm, deviceName, DeviceConnectionState.Disconnected, TimeSpan.FromSeconds(60));

                // Act
                // Send message before device is listening
                Message message = this.CreateMessage(out string payload);
                await serviceClient.SendAsync(deviceName, message);

                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);

                // Assert
                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);

                // Act
                // send new message after offline was received
                message = this.CreateMessage(out payload);
                await serviceClient.SendAsync(deviceName, message);

                // Assert
                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);
            }
            finally
            {
                await this.Cleanup(deviceClient, serviceClient, rm, deviceName);
            }
        }

        [Fact]
        [TestPriority(103)]
        public async void Receive_C2D_NotSubscribed_OfflineSingleMessage_ShouldThrow()
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string edgeDeviceId = ConnectionStringHelper.GetDeviceId(ConfigHelper.TestConfig[Service.Constants.ConfigKey.IotHubConnectionString]);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            var edgeDevice = await rm.GetDeviceAsync(edgeDeviceId);
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, rm, scope: edgeDevice.Scope);

            ServiceClient serviceClient = null;
            DeviceClient deviceClient = null;
            try
            {
                serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);
                await serviceClient.OpenAsync();

                // Act
                // Send message before device is listening
                Message message = this.CreateMessage(out string payload);
                await serviceClient.SendAsync(deviceName, message);

                // Wait to make sure message is not received because of ClockSkewAdjustment
                await Task.Delay(ClockSkewAdjustment);

                ITransportSettings[] settings = this.GetTransportSettings();
                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, settings);
                await deviceClient.OpenAsync();

                // Assert
                await Assert.ThrowsAsync<TimeoutException>(() => this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]));
            }
            finally
            {
                await this.Cleanup(deviceClient, serviceClient, rm, deviceName);
            }
        }

        ITransportSettings[] GetTransportSettings(TransportType transportType = TransportType.Mqtt_Tcp_Only)
        {
            var mqttSetting = new MqttTransportSettings(transportType)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            ITransportSettings[] settings = { mqttSetting };
            return settings;
        }

        async Task WaitForDeviceConnectionStateTimeoutAfter(RegistryManager rm, string deviceName, DeviceConnectionState state, TimeSpan timespan)
        {
            Task timerTask = Task.Delay(timespan);
            CancellationTokenSource cts = new CancellationTokenSource();

            Task completedTask = await Task.WhenAny(this.WaitForDeviceConnectionState(rm, deviceName, state, cts.Token), timerTask);
            if (completedTask == timerTask)
            {
                cts.Cancel();
                throw new TimeoutException(string.Format("Wait for device to be in {0} state timed out", state));
            }
        }

        async Task WaitForDeviceConnectionState(RegistryManager rm, string deviceName, DeviceConnectionState state, CancellationToken cancellationToken)
        {
            var device = await rm.GetDeviceAsync(deviceName);
            while (device.ConnectionState != state && !cancellationToken.IsCancellationRequested)
            {
                device = await rm.GetDeviceAsync(deviceName);
                await Task.Delay(TimeSpan.FromSeconds(5));
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

        [AssertionMethod]
        async Task VerifyReceivedC2DMessage(DeviceClient deviceClient, string payload, string p1Value)
        {
            Client.Message receivedMessage = await deviceClient.ReceiveAsync();

            if (receivedMessage != null)
            {
                string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                Assert.Equal(messageData, payload);
                Assert.Equal(1, receivedMessage.Properties.Count);
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

        async Task Cleanup(DeviceClient deviceClient, ServiceClient serviceClient, RegistryManager rm, string deviceName)
        {
            if (deviceClient != null)
            {
                try
                {
                    await deviceClient.CloseAsync();
                }
                catch
                {
                    // ignore
                }
            }

            if (serviceClient != null)
            {
                await serviceClient.CloseAsync();
            }

            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(20));

            if (rm != null)
            {
                await RegistryManagerHelper.RemoveDevice(deviceName, rm);
                await rm.CloseAsync();
            }
        }
    }
}
