// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class TelemetryTest
    {
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 10;
            TestModule sender = null;
            TestModule receiver = null;

            string iothubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iothubConnectionString);
            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", transportSettings);

                await receiver.SetupReceiveMessageHandler();

                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));

                int sentMessagesCount = await task1;
                Assert.Equal(messagesCount, sentMessagesCount);

                await Task.Delay(TimeSpan.FromSeconds(5));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();

                Assert.Equal(messagesCount, receivedMessages.Count);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }

                if (sender != null)
                {
                    await sender.Disconnect();
                }

                if (receiver != null)
                {
                    await receiver.Disconnect();
                }
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendOneTelemetryMessageTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 1;
            TestModule sender = null;
            TestModule receiver = null;

            string iothubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iothubConnectionString);
            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender3", transportSettings);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver3", transportSettings);

                await receiver.SetupReceiveMessageHandler();

                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));

                int sentMessagesCount = await task1;
                Assert.Equal(messagesCount, sentMessagesCount);

                await Task.Delay(TimeSpan.FromSeconds(3));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();

                Assert.Equal(messagesCount, receivedMessages.Count);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }

                if (sender != null)
                {
                    await sender.Disconnect();
                }

                if (receiver != null)
                {
                    await receiver.Disconnect();
                }
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryMultipleInputsTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 30;
            TestModule sender = null;
            TestModule receiver = null;

            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender11", transportSettings);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver11", transportSettings);

                await receiver.SetupReceiveMessageHandler("input1");
                await receiver.SetupReceiveMessageHandler("input2");

                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(2));
                Task<int> task2 = sender.SendMessagesByCountAsync("output2", 0, messagesCount, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(3));

                int[] sentMessagesCounts = await Task.WhenAll(task1, task2);
                Assert.Equal(messagesCount, sentMessagesCounts[0]);
                Assert.Equal(messagesCount, sentMessagesCounts[1]);

                await Task.Delay(TimeSpan.FromSeconds(20));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices("input1");
                Assert.Equal(messagesCount, receivedMessages.Count);

                receivedMessages = receiver.GetReceivedMessageIndices("input2");
                Assert.Equal(messagesCount, receivedMessages.Count);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }

                if (sender != null)
                {
                    await sender.Disconnect();
                }

                if (receiver != null)
                {
                    await receiver.Disconnect();
                }
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendLargeMessageHandleExceptionTest(ITransportSettings[] transportSettings)
        {
            TestModule sender = null;

            string iothubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iothubConnectionString);
            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender4", transportSettings);

                Exception ex = null;
                try
                {
                    // create a large message
                    var message = new Message(new byte[400 * 1000]);
                    await sender.SendMessageAsync("output1", message);
                }
                catch (Exception e)
                {
                    ex = e;
                }

                Assert.NotNull(ex);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryWithDelayedReceiverTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 10;
            TestModule sender = null;
            TestModule receiver = null;

            string iothubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iothubConnectionString);
            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender5", transportSettings);
                int sentMessagesCount = await sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));
                Assert.Equal(messagesCount, sentMessagesCount);

                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver5", transportSettings);
                await receiver.SetupReceiveMessageHandler();

                await Task.Delay(TimeSpan.FromSeconds(60));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();

                Assert.Equal(messagesCount, receivedMessages.Count);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }

                if (sender != null)
                {
                    await sender.Disconnect();
                }

                if (receiver != null)
                {
                    await receiver.Disconnect();
                }
            }
        }
    }
}
