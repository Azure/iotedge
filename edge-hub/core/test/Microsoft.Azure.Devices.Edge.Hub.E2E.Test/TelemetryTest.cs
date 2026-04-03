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
        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryTest(IotHubClientOptions clientOptions)
        {
            int messagesCount = 10;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", clientOptions);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", clientOptions);

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
                    rm.Dispose();
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

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendOneTelemetryMessageTest(IotHubClientOptions clientOptions)
        {
            int messagesCount = 1;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", clientOptions);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", clientOptions);

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
                    rm.Dispose();
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

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryMultipleInputsTest(IotHubClientOptions clientOptions)
        {
            int messagesCount = 30;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender11", clientOptions);
                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver11", clientOptions);

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
                    rm.Dispose();
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
        async Task SendLargeMessageHandleExceptionTest(IotHubClientOptions clientOptions)
        {
            TestModule sender = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", clientOptions);

                Exception ex = null;
                try
                {
                    // create a large message
                    var message = new TelemetryMessage(new byte[400 * 1000]);
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
                    rm.Dispose();
                }
            }

            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        [Theory(Skip = "Flaky")]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task SendTelemetryWithDelayedReceiverTest(IotHubClientOptions clientOptions)
        {
            int messagesCount = 10;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", clientOptions);
                int sentMessagesCount = await sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));
                Assert.Equal(messagesCount, sentMessagesCount);

                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", clientOptions);
                await receiver.SetupReceiveMessageHandler();

                await Task.Delay(TimeSpan.FromSeconds(60));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();

                Assert.Equal(messagesCount, receivedMessages.Count);
            }
            finally
            {
                if (rm != null)
                {
                    rm.Dispose();
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
    }
}
