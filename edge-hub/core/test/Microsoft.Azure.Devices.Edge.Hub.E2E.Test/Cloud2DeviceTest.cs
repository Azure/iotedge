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
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class Cloud2DeviceTest
    {
        const string MessagePropertyName = "property1";
        const string DeviceNamePrefix = "E2E_c2d_";
        static readonly TimeSpan ClockSkewAdjustment = TimeSpan.FromSeconds(35);

        [Theory(Skip = "Flaky")]
        [TestPriority(101)]
        [InlineData(IotHubClientTransportProtocol.Tcp)]
        // [InlineData(IotHubClientTransportProtocol.WebSocket)] // Disabled: need a valid server cert for WebSocket to work
        public async Task Receive_C2D_SingleMessage_ShouldSucceed(IotHubClientTransportProtocol protocol)
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string edgeDeviceId = ConnectionStringHelper.GetDeviceId(ConfigHelper.TestConfig[Service.Constants.ConfigKey.IotHubConnectionString]);
            IotHubServiceClient serviceClient = new IotHubServiceClient(iotHubConnectionString);
            var edgeDevice = await serviceClient.Devices.GetAsync(edgeDeviceId);
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, serviceClient, scope: edgeDevice.Scope);

            IotHubDeviceClient deviceClient = null;
            try
            {
                IotHubClientOptions options = this.GetClientOptions(protocol);
                deviceClient = IotHubDeviceClient.CreateFromConnectionString(deviceConnectionString, options);
                // Dummy ReceiveAsync to ensure mqtt subscription registration before SendAsync() is called on service client.
                await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(2));

                // Act
                OutgoingMessage message = this.CreateMessage(out string payload);
                await serviceClient.Messages.SendAsync(deviceName, message);

                // Assert
                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);
            }
            finally
            {
                await this.Cleanup(deviceClient, serviceClient, deviceName);
            }
        }

        [Fact(Skip = "Flaky")]
        [TestPriority(102)]
        public async Task Receive_C2D_SingleMessage_AfterOfflineMessage_ShouldSucceed()
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string edgeDeviceId = ConnectionStringHelper.GetDeviceId(ConfigHelper.TestConfig[Service.Constants.ConfigKey.IotHubConnectionString]);
            IotHubServiceClient serviceClient = new IotHubServiceClient(iotHubConnectionString);
            var edgeDevice = await serviceClient.Devices.GetAsync(edgeDeviceId);

            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, serviceClient, scope: edgeDevice.Scope);

            IotHubDeviceClient deviceClient = null;
            try
            {
                IotHubClientOptions options = this.GetClientOptions();
                deviceClient = IotHubDeviceClient.CreateFromConnectionString(deviceConnectionString, options);
                // Dummy ReceiveAsync to ensure mqtt subscription registration before SendAsync() is called on service client.
                await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(1));

                var device = await serviceClient.Devices.GetAsync(deviceName);
                // Wait for device to be connected to cloud
                await this.WaitForDeviceConnectionStateTimeoutAfter(serviceClient, deviceName, DeviceConnectionState.Connected, TimeSpan.FromSeconds(60));
                await deviceClient.CloseAsync();

                // Wait for the connection to be closed on the Edge side by checking device connection state
                await this.WaitForDeviceConnectionStateTimeoutAfter(serviceClient, deviceName, DeviceConnectionState.Disconnected, TimeSpan.FromSeconds(60));

                // Act
                // Send message before device is listening
                OutgoingMessage message = this.CreateMessage(out string payload);
                await serviceClient.Messages.SendAsync(deviceName, message);

                deviceClient = IotHubDeviceClient.CreateFromConnectionString(deviceConnectionString, options);

                // Assert
                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);

                // Act
                // send new message after offline was received
                message = this.CreateMessage(out payload);
                await serviceClient.Messages.SendAsync(deviceName, message);

                // Assert
                await this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]);
            }
            finally
            {
                await this.Cleanup(deviceClient, serviceClient, deviceName);
            }
        }

        [Fact]
        [TestPriority(103)]
        public async Task Receive_C2D_NotSubscribed_OfflineSingleMessage_ShouldThrow()
        {
            // Arrange
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            string edgeDeviceId = ConnectionStringHelper.GetDeviceId(ConfigHelper.TestConfig[Service.Constants.ConfigKey.IotHubConnectionString]);
            IotHubServiceClient serviceClient = new IotHubServiceClient(iotHubConnectionString);
            var edgeDevice = await serviceClient.Devices.GetAsync(edgeDeviceId);
            (string deviceName, string deviceConnectionString) = await RegistryManagerHelper.CreateDevice(DeviceNamePrefix, iotHubConnectionString, serviceClient, scope: edgeDevice.Scope);

            IotHubDeviceClient deviceClient = null;
            try
            {
                // Act
                // Send message before device is listening
                OutgoingMessage message = this.CreateMessage(out string payload);
                await serviceClient.Messages.SendAsync(deviceName, message);

                // Wait to make sure message is not received because of ClockSkewAdjustment
                await Task.Delay(ClockSkewAdjustment);

                IotHubClientOptions options = this.GetClientOptions();
                deviceClient = IotHubDeviceClient.CreateFromConnectionString(deviceConnectionString, options);
                await deviceClient.OpenAsync();

                // Assert
                await Assert.ThrowsAsync<TimeoutException>(() => this.VerifyReceivedC2DMessage(deviceClient, payload, message.Properties[MessagePropertyName]));
            }
            finally
            {
                await this.Cleanup(deviceClient, serviceClient, deviceName);
            }
        }

        IotHubClientOptions GetClientOptions(IotHubClientTransportProtocol protocol = IotHubClientTransportProtocol.Tcp)
        {
            var mqttSetting = new IotHubClientMqttSettings(protocol)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            return new IotHubClientOptions(mqttSetting);
        }

        async Task WaitForDeviceConnectionStateTimeoutAfter(IotHubServiceClient serviceClient, string deviceName, DeviceConnectionState state, TimeSpan timespan)
        {
            Task timerTask = Task.Delay(timespan);
            CancellationTokenSource cts = new CancellationTokenSource();

            Task completedTask = await Task.WhenAny(this.WaitForDeviceConnectionState(serviceClient, deviceName, state, cts.Token), timerTask);
            if (completedTask == timerTask)
            {
                cts.Cancel();
                throw new TimeoutException(string.Format("Wait for device to be in {0} state timed out", state));
            }
        }

        async Task WaitForDeviceConnectionState(IotHubServiceClient serviceClient, string deviceName, DeviceConnectionState state, CancellationToken cancellationToken)
        {
            var device = await serviceClient.Devices.GetAsync(deviceName);
            while (device.ConnectionState != state && !cancellationToken.IsCancellationRequested)
            {
                device = await serviceClient.Devices.GetAsync(deviceName);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        OutgoingMessage CreateMessage(out string payload)
        {
            payload = Guid.NewGuid().ToString();
            string messageId = Guid.NewGuid().ToString();
            string property1Value = Guid.NewGuid().ToString();

            var message = new OutgoingMessage(Encoding.UTF8.GetBytes(payload))
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
        async Task VerifyReceivedC2DMessage(IotHubDeviceClient deviceClient, string payload, string p1Value)
        {
            var receivedMessage = await deviceClient.ReceiveAsync();

            if (receivedMessage != null)
            {
                string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                Assert.Equal(payload, messageData);
                Assert.Equal(1, receivedMessage.Properties.Count);
                KeyValuePair<string, string> prop = receivedMessage.Properties.Single();
                Assert.Equal(MessagePropertyName, prop.Key);
                Assert.Equal(p1Value, prop.Value);

                await deviceClient.CompleteAsync(receivedMessage);
            }
            else
            {
                throw new TimeoutException("Test is running longer than expected.");
            }
        }

        async Task Cleanup(IotHubDeviceClient deviceClient, IotHubServiceClient serviceClient, string deviceName)
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

            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(20));

            if (serviceClient != null)
            {
                await RegistryManagerHelper.RemoveDevice(deviceName, serviceClient);
                serviceClient.Dispose();
            }
        }
    }
}
