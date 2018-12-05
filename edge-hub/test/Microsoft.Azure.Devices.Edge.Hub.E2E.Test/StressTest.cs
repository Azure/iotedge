// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration, Stress]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class StressTest : IClassFixture<ProtocolHeadFixture>
    {        
        [TestPriority(301)]
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task SingleSenderSingleReceiverTest(ITransportSettings[] transportSettings)
        {
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_SingleSender"], out int messagesCount);
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", false, transportSettings);
                receiver = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", true, transportSettings);

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
            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        [TestPriority(302)]
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task MultipleSendersSingleReceiverTest(ITransportSettings[] transportSettings)
        {
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_MultipleSenders"], out int messagesCount);
            TestModule sender1 = null;
            TestModule sender2 = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender1 = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "senderA", false, transportSettings);
                sender2 = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "senderB", false, transportSettings);
                receiver = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiverA", true, transportSettings);

                Task<int> task1 = sender1.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(8));
                Task<int> task2 = sender2.SendMessagesByCountAsync("output1", messagesCount, messagesCount, TimeSpan.FromMinutes(8));

                int[] results = await Task.WhenAll(task1, task2);
                int sentMessagesCount = results.Sum();
                Assert.Equal(messagesCount * 2, sentMessagesCount);

                await Task.Delay(TimeSpan.FromSeconds(20));
                ISet<int> receivedMessages = receiver.GetReceivedMessageIndices();

                Assert.Equal(sentMessagesCount, receivedMessages.Count);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }
                if (sender1 != null)
                {
                    await sender1.Disconnect();
                }
                if (sender2 != null)
                {
                    await sender2.Disconnect();
                }
                if (receiver != null)
                {
                    await receiver.Disconnect();
                }
            }
            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        [TestPriority(303)]
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task MultipleSendersMultipleReceivers_Count_Test(ITransportSettings[] transportSettings)
        {
            // The modules limit is because ProtocolGatewayFixture currently uses a fixed EdgeDevice
            // Need to figure out a way to create ProtocolGatewayFixture with configurable EdgeDevice
            const int ModulesCount = 2;
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_MultipleSendersMultipleReceivers"], out int messagesCount);
            List<TestModule> senders = null;
            List<TestModule> receivers = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                senders = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender", ModulesCount, false, transportSettings);
                receivers = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver", ModulesCount, true, transportSettings);

                TimeSpan timeout = TimeSpan.FromMinutes(2);
                IEnumerable<Task<int>> tasks = senders.Select(s => s.SendMessagesByCountAsync("output1", 0, messagesCount, timeout));

                int[] results = await Task.WhenAll(tasks);
                int sentMessagesCount = results.Sum();
                Assert.Equal(messagesCount * ModulesCount, sentMessagesCount);

                await Task.Delay(TimeSpan.FromSeconds(20));
                int receivedMessagesCount = 0;
                receivers.ForEach(r => receivedMessagesCount += r.GetReceivedMessageIndices().Count);

                Assert.Equal(sentMessagesCount, receivedMessagesCount);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }
                if (senders != null)
                {
                    await Task.WhenAll(senders.Select(s => s.Disconnect()));
                }
                if (receivers != null)
                {
                    await Task.WhenAll(receivers.Select(r => r.Disconnect()));
                }
                await (rm?.CloseAsync() ?? Task.CompletedTask);
            }
            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        [TestPriority(304)]
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task MultipleSendersMultipleReceivers_Duration_Test(ITransportSettings[] transportSettings)
        {
            // The modules limit is because ProtocolGatewayFixture currently uses a fixed EdgeDevice
            // Need to figure out a way to create ProtocolGatewayFixture with configurable EdgeDevice
            const int ModulesCount = 2;
            List<TestModule> senders = null;
            List<TestModule> receivers = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                senders = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender", ModulesCount, false, transportSettings);
                receivers = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver", ModulesCount, true, transportSettings);

                TimeSpan sendDuration = TimeSpan.FromMinutes(2);
                IEnumerable<Task<int>> tasks = senders.Select(s => s.SendMessagesForDurationAsync("output1", sendDuration));

                int[] results = await Task.WhenAll(tasks);
                int sentMessagesCount = results.Sum();

                await Task.Delay(TimeSpan.FromSeconds(20));
                int receivedMessagesCount = 0;
                receivers.ForEach(r => receivedMessagesCount += r.GetReceivedMessageIndices().Count);

                Assert.Equal(sentMessagesCount, receivedMessagesCount);
            }
            finally
            {
                if (rm != null)
                {
                    await rm.CloseAsync();
                }
                if (senders != null)
                {
                    await Task.WhenAll(senders.Select(s => s.Disconnect()));
                }
                if (receivers != null)
                {
                    await Task.WhenAll(receivers.Select(r => r.Disconnect()));
                }
                await (rm?.CloseAsync() ?? Task.CompletedTask);
            }
            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        async Task<List<TestModule>> GetModules(RegistryManager rm, string hostName, string deviceId, string moduleNamePrefix, int count, bool isReceiver, ITransportSettings[] transportSettings)
        {
            var modules = new List<TestModule>();
            for (int i = 1; i <= count; i++)
            {
                string moduleId = moduleNamePrefix + i.ToString();
                TestModule module = await this.GetModule(rm, hostName, deviceId, moduleId, isReceiver, transportSettings);
                modules.Add(module);
            }
            return modules;
        }

        async Task<TestModule> GetModule(RegistryManager rm, string hostName, string deviceId, string moduleId, bool isReceiver, ITransportSettings[] transportSettings)
        {
            string connStr = await RegistryManagerHelper.GetOrCreateModule(rm, hostName, deviceId, moduleId);
            TestModule module = await TestModule.CreateAndConnect(connStr, transportSettings);
            if (isReceiver)
            {
                await module.SetupReceiveMessageHandler();
            }
            return module;
        }         
    }
}
