// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    public class BackupAndRestoreTest : IDisposable
    {
        readonly string backupFolder;
        IList<ProtocolHeadFixture> fixtures = new List<ProtocolHeadFixture>();

        public BackupAndRestoreTest()
        {
            string tempFolder = Path.GetTempPath();
            this.backupFolder = Path.Combine(tempFolder, $"edgeTestBackup{Guid.NewGuid()}");
            if (Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder);
            }

            Directory.CreateDirectory(this.backupFolder);

            ConfigHelper.TestConfig["UsePersistentStorage"] = "false";
            ConfigHelper.TestConfig["BackupFolder"] = this.backupFolder;
            ConfigHelper.TestConfig["EnableStorageBackupAndRestore"] = "true";
        }

        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(this.backupFolder) && Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder, true);
            }

            foreach (ProtocolHeadFixture fixture in this.fixtures)
            {
                fixture.Dispose();
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        async Task BackupAndRestoreMessageDeliveryTest(ITransportSettings[] transportSettings)
        {
            ProtocolHeadFixture protocolHeadFixture = this.GetFixture();
            int messagesCount = 10;
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            //ProtocolHeadFixture originalFixture = protocolHeadFixture;
            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);

                // Send 10 messages before a receiver is registered.
                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));
                int sentMessagesCount = await task1;
                Assert.Equal(messagesCount, sentMessagesCount);

                // Wait for a while and then close the test fixture which will in turn close the protocol heads and the in-memory DB store thus creating a backup.
                await Task.Delay(TimeSpan.FromSeconds(5));
                //await this.protocolHeadFixture.CloseAsync();
                //this.protocolHeadFixture = new ProtocolHeadFixture();

                await protocolHeadFixture.CloseAsync();
                //protocolHeadFixture = new ProtocolHeadFixture();
                protocolHeadFixture = this.GetFixture();

                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", transportSettings);
                await receiver.SetupReceiveMessageHandler();

                // Send 10 more messages after the receiver is registered.
                Task<int> task2 = sender.SendMessagesByCountAsync("output1", messagesCount, messagesCount, TimeSpan.FromMinutes(2));
                sentMessagesCount = await task2;
                Assert.Equal(messagesCount, sentMessagesCount);

                // Validate that all the messages were received.
                await Task.Delay(TimeSpan.FromSeconds(5));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();

                Assert.Equal(messagesCount * 2, receivedMessages.Count);
            }
            finally
            {
                ConfigHelper.TestConfig["UsePersistentStorage"] = null;
                ConfigHelper.TestConfig["BackupFolder"] = null;
                ConfigHelper.TestConfig["EnableStorageBackupAndRestore"] = null;

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

                //protocolHeadFixture.Dispose();
                //originalFixture.Dispose();
            }

            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        private ProtocolHeadFixture GetFixture()
        {
            ProtocolHeadFixture fixture = new ProtocolHeadFixture();
            this.fixtures.Add(fixture);
            return fixture;
        }
    }
}
