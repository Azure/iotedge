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
    using static Microsoft.Azure.Devices.Edge.Hub.E2E.Test.ProtocolHeadFixture;

    public abstract class StoreLimitsTestBase : IDisposable
    {
        const int MessageSize = 1024;
        const int DefaultMessageCount = 10;
        const int MaxStorageSize = MessageSize * DefaultMessageCount;
        InternalProtocolHeadFixture fixture;

        public StoreLimitsTestBase(bool usePersistentStorage)
        {
            ConfigHelper.TestConfig["UsePersistentStorage"] = usePersistentStorage.ToString();
            ConfigHelper.TestConfig["MaxStorageBytes"] = MaxStorageSize.ToString();
            ConfigHelper.TestConfig["TimeToLiveSecs"] = "20";
            this.fixture = new ProtocolHeadFixture.InternalProtocolHeadFixture();
        }

        public void Dispose()
        {
            this.fixture.CloseAsync().Wait();
            ConfigHelper.TestConfig["UsePersistentStorage"] = null;
            ConfigHelper.TestConfig["MaxStorageBytes"] = null;
        }

        static ITransportSettings[] StoreLimitTestTransportSettings = TestSettings.AmqpTransportSettings;

        protected async Task StoreLimitValidationTestAsync()
        {
            int messagesCount = DefaultMessageCount;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", StoreLimitTestTransportSettings, 0);
                string connStr = await RegistryManagerHelper.GetOrCreateModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1");
                receiver = await TestModule.CreateAndConnect(connStr, StoreLimitTestTransportSettings, 0);
                await receiver.SetupReceiveMessageHandlerWithNoCompletion();

                // Send messages to ensure that the max storage size limit is reached.
                //await sender.SendMessagesByCountAndSizeAsync("output1", 0, messagesCount, MessageSize, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(2));
                int sentMessagesCount = await sender.SendMessagesByCountAndSizeAsync("output1", 0, messagesCount, MessageSize, TimeSpan.FromMinutes(2));
                Assert.Equal(messagesCount, sentMessagesCount);

                // Wait for 10 seconds to let the space checker run.
                await Task.Delay(TimeSpan.FromSeconds(10));

                // Sending 1 more message should result in failure as the storage size limit has been reached.
                await Assert.ThrowsAsync<IotHubThrottledException>(() => sender.SendMessagesByCountAndSizeAsync("output1", 0, 1, MessageSize, TimeSpan.FromMinutes(2)));

                //await receiver.Connect();
                await receiver.SetupReceiveMessageHandler();
                //receiver = await TestModule.CreateAndConnect(connStr, StoreLimitTestTransportSettings, 0);
                //await receiver.SetupReceiveMessageHandler();
                await Task.Delay(TimeSpan.FromSeconds(30));

                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();
                Assert.Equal(messagesCount, receivedMessages.Count);

                // Wait some more time to allow the delivered messages to be cleaned up.
                await Task.Delay(TimeSpan.FromSeconds(120));

                // Sending a few more messages should now succeed.
                messagesCount = 2;
                sentMessagesCount = await sender.SendMessagesByCountAndSizeAsync("output1", 0, messagesCount, MessageSize, TimeSpan.FromMinutes(2));
                Assert.Equal(messagesCount, sentMessagesCount);
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
    }
}
