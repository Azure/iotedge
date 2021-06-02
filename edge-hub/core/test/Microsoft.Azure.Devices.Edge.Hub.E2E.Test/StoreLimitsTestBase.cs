// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    public abstract class StoreLimitsTestBase : IDisposable
    {
        const int BufferSize = 2000;
        const int MessageSize = 1024;
        const int DefaultMessageCount = 10;
        const int MaxStorageSize = MessageSize * DefaultMessageCount + BufferSize;
        ProtocolHeadFixture protocolHeadFixture;

        public StoreLimitsTestBase(bool usePersistentStorage)
        {
            ConfigHelper.TestConfig["UsePersistentStorage"] = usePersistentStorage.ToString();
            ConfigHelper.TestConfig["MaxStorageBytes"] = MaxStorageSize.ToString();
            ConfigHelper.TestConfig["TimeToLiveSecs"] = "0";
            ConfigHelper.TestConfig["messageCleanupIntervalSecs"] = "10";
            ConfigHelper.TestConfig["Routes"] = JsonConvert.SerializeObject(Routes);
            this.protocolHeadFixture = EdgeHubFixtureCollection.GetFixture();
        }

        public void Dispose()
        {
            this.protocolHeadFixture.CloseAsync().Wait();
            ConfigHelper.TestConfig["UsePersistentStorage"] = null;
            ConfigHelper.TestConfig["MaxStorageBytes"] = null;
            ConfigHelper.TestConfig["TimeToLiveSecs"] = null;
            ConfigHelper.TestConfig["Routes"] = null;
        }

        static readonly ITransportSettings[] StoreLimitTestTransportSettings = TestSettings.AmqpTransportSettings;

        /// <summary>
        /// This has been created to override the default routes passed by the test `DependencyManager` to the
        /// EdgeHub's modules during initialization. The default routes/endpoints are too many for the cleanup processor in
        /// Edge Hub to go through (to cleanup stale messages) and it would not be viable to wait for that much time
        /// in the tests of this class so that store limits can be easily tested.
        /// </summary>
        static readonly IDictionary<string, string> Routes = new Dictionary<string, string>()
        {
            ["r1"] = "FROM /messages/modules/sender1forstorelimits INTO BrokeredEndpoint(\"/modules/receiver1/inputs/input1\")",
        };

        protected async Task StoreLimitValidationTestAsync()
        {
            int messagesCount = DefaultMessageCount;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);
            Guid guid = Guid.NewGuid();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1forstorelimits", StoreLimitTestTransportSettings, 0);

                // Send messages to ensure that the max storage size limit is reached.
                int sentMessagesCount = 0;
                await Assert.ThrowsAsync<IotHubThrottledException>(
                    async () => sentMessagesCount = await sender.SendMessagesByCountAndSizeAsync("output1", 0, messagesCount, MessageSize, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(2)));

                // Wait some more time to allow the delivered messages to be cleaned up due to TTL expiry.
                await Task.Delay(TimeSpan.FromSeconds(60));

                // Sending a few more messages should now succeed.
                messagesCount = 2;
                sentMessagesCount = await sender.SendMessagesByCountAndSizeAsync("output1", 0, messagesCount, MessageSize, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(2));
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
