// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    /// <summary>
    /// Note: All tests in this class are using just AMQP test settings for now.
    /// This is because there is an issue with the IoT Device SDK where when a ModuleClient
    /// is re-created to send messages after the protocol head has been closed once (to trigger
    /// backups), using the new module client to send messages gets stuck in an endless loop with no
    /// response. Needs to be investigate further.
    ///
    /// The tests in this class are not dependent on the chosen protocol and therefore just validation
    /// using AMQP should suffice for now.
    /// </summary>
    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.EdgeHubCollection.Test")]
    public class BackupAndRestoreTest : IDisposable
    {
        readonly string backupFolder;
        EdgeHubFixture edgeHubFixture;

        public BackupAndRestoreTest(EdgeHubFixture edgeHubFixture)
        {
            Console.WriteLine("Constructor");
            this.edgeHubFixture = edgeHubFixture;
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
            Console.WriteLine("Dispose");
            ConfigHelper.TestConfig["UsePersistentStorage"] = null;
            ConfigHelper.TestConfig["BackupFolder"] = null;
            ConfigHelper.TestConfig["EnableStorageBackupAndRestore"] = null;

            if (!string.IsNullOrWhiteSpace(this.backupFolder) && Directory.Exists(this.backupFolder))
            {
                Directory.Delete(this.backupFolder, true);
            }
        }

        [Theory]
        [MemberData(nameof(TestSettings.AmqpTransportTestSettings), MemberType = typeof(TestSettings))]
        async Task BackupAndRestoreMessageDeliveryTest(ITransportSettings[] transportSettings)
        {
            await this.BackupAndRestoreMessageDeliveryTestBase(transportSettings, 10, 10, 20, () => { });
        }

        [Theory]
        [MemberData(nameof(TestSettings.AmqpTransportTestSettings), MemberType = typeof(TestSettings))]
        async Task BackupAndRestoreLargeBackupSizeTest(ITransportSettings[] transportSettings)
        {
            int.TryParse(ConfigHelper.TestConfig["BackupAndRestoreLargeBackupSize_MessagesCount_SingleSender"], out int messagesCount);
            await this.BackupAndRestoreMessageDeliveryTestBase(transportSettings, messagesCount, 10, messagesCount + 10, () => { });
        }

        [Theory]
        [MemberData(nameof(TestSettings.AmqpTransportTestSettings), MemberType = typeof(TestSettings))]
        async Task BackupAndRestoreCorruptBackupMetadataTest(ITransportSettings[] transportSettings)
        {
            Action corruptBackupMetadata = () =>
            {
                // Corrupt the backup metadata.
                using (FileStream file = File.OpenWrite(Path.Combine(this.backupFolder, "meta.json")))
                {
                    file.Write(new byte[] { 1, 2 }, 1, 1);
                }
            };
            
            await this.BackupAndRestoreMessageDeliveryTestBase(transportSettings, 15, 10, 10, corruptBackupMetadata);
        }

        [Theory]
        [MemberData(nameof(TestSettings.AmqpTransportTestSettings), MemberType = typeof(TestSettings))]
        async Task BackupAndRestoreCorruptBackupDataTest(ITransportSettings[] transportSettings)
        {
            Action corruptBackupMetadata = () =>
            {
                // Corrupt the backup data.
                DirectoryInfo[] directories = new DirectoryInfo(this.backupFolder).GetDirectories();
                DirectoryInfo newBackupDir = directories.Where(x => x.GetFiles().Count() > 0).ToArray()[0];
                FileInfo someBackupFile = newBackupDir.GetFiles()[0];
                using (FileStream file = someBackupFile.OpenWrite())
                {
                    file.Write(new byte[] { 1, 2 }, 1, 1);
                }
            };

            await this.BackupAndRestoreMessageDeliveryTestBase(transportSettings, 15, 10, 10, corruptBackupMetadata);
        }

        async Task BackupAndRestoreMessageDeliveryTestBase(
            ITransportSettings[] transportSettings,
            int beforeBackupMessageCount,
            int afterBackupMessageCount,
            int expectedMessageCountAfterRestore,
            Action postBackupModifier)
        {
            ProtocolHeadFixture protocolHeadFixture = this.edgeHubFixture.GetFixture();
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", transportSettings);

                // Send 10 messages before a receiver is registered.
                Task<int> task1 = sender.SendMessagesByCountAsync("output1", 0, beforeBackupMessageCount, TimeSpan.FromMinutes(10));
                int sentMessagesCount = await task1;
                Assert.Equal(beforeBackupMessageCount, sentMessagesCount);

                // Wait for a while and then close the test fixture which will in turn close the protocol heads and the in-memory DB store thus creating a backup.
                await Task.Delay(TimeSpan.FromSeconds(5));
                await protocolHeadFixture.CloseAsync();

                postBackupModifier();

                // Get new fixture to re-initialize the edge hub container.
                protocolHeadFixture = this.edgeHubFixture.GetFixture();

                receiver = await TestModule.CreateAndConnect(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", transportSettings);
                await receiver.SetupReceiveMessageHandler();

                // Send more messages after the receiver is registered.
                Task<int> task2 = sender.SendMessagesByCountAsync("output1", beforeBackupMessageCount, afterBackupMessageCount, TimeSpan.FromMinutes(2));
                sentMessagesCount = await task2;
                Assert.Equal(afterBackupMessageCount, sentMessagesCount);

                // Validate that all the messages were received (both sent earlier and the new messages).
                await Task.Delay(TimeSpan.FromSeconds(5));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();

                Assert.Equal(expectedMessageCountAfterRestore, receivedMessages.Count);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                    rm.Dispose();
                }

                if (sender != null)
                {
                    await sender.Disconnect();
                    sender.Dispose();
                }

                if (receiver != null)
                {
                    await receiver.Disconnect();
                    receiver.Dispose();
                }

                await protocolHeadFixture.CloseAsync();
            }

            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
