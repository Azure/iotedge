// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [E2E]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class TelemetryTest : IClassFixture<ProtocolHeadFixture>
    {
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 10;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", transportSettings);

                await receiver.SetupReceiveMessageHandler();

                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));

                int sentMessagesCount = await task1;
                Assert.Equal(messagesCount, sentMessagesCount);

                await Task.Delay(TimeSpan.FromSeconds(20));
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
            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryMultipleInputsTest(ITransportSettings[] transportSettings)
        {
            int messagesCount = 30;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

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
            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryHandleExceptionTest(ITransportSettings[] transportSettings)
        {
            TestModule sender = null;
            
            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);

                // create a large message
                var message = new Message(new byte[400]);
                await Assert.ThrowsAsync<IotHubException>(() => sender.SendMessageAsync("output1", message));
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
            }
            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
