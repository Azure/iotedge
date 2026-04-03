// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Stress]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class StressTest
    {
        [TestPriority(Constants.TestPriority.StressTest.SingleSenderSingleReceiverTest)]
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task SingleSenderSingleReceiverTest(IotHubClientOptions clientOptions)
        {
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_SingleSender"], out int messagesCount);
            TestModule sender = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                sender = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", false, clientOptions);
                receiver = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", true, clientOptions);

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
            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        [TestPriority(Constants.TestPriority.StressTest.MultipleSendersSingleReceiverTest)]
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task MultipleSendersSingleReceiverTest(IotHubClientOptions clientOptions)
        {
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_MultipleSenders"], out int messagesCount);
            TestModule sender1 = null;
            TestModule sender2 = null;
            TestModule receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                sender1 = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "senderA", false, clientOptions);
                sender2 = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "senderB", false, clientOptions);
                receiver = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiverA", true, clientOptions);

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
                    rm.Dispose();
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

        [TestPriority(Constants.TestPriority.StressTest.MultipleSendersMultipleReceivers_Count_Test)]
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task MultipleSendersMultipleReceivers_Count_Test(IotHubClientOptions clientOptions)
        {
            // The modules limit is because ProtocolGatewayFixture currently uses a fixed EdgeDevice
            // Need to figure out a way to create ProtocolGatewayFixture with configurable EdgeDevice
            const int ModulesCount = 2;
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_MultipleSendersMultipleReceivers"], out int messagesCount);
            List<TestModule> senders = null;
            List<TestModule> receivers = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                senders = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender", ModulesCount, false, clientOptions);
                receivers = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver", ModulesCount, true, clientOptions);

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
                    rm.Dispose();
                }

                if (senders != null)
                {
                    await Task.WhenAll(senders.Select(s => s.Disconnect()));
                }

                if (receivers != null)
                {
                    await Task.WhenAll(receivers.Select(r => r.Disconnect()));
                }
            }

            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        [TestPriority(Constants.TestPriority.StressTest.MultipleSendersMultipleReceivers_Duration_Test)]
        [Theory]
        [MemberData(nameof(TestSettings.TransportSettings), MemberType = typeof(TestSettings))]
        public async Task MultipleSendersMultipleReceivers_Duration_Test(IotHubClientOptions clientOptions)
        {
            // The modules limit is because ProtocolGatewayFixture currently uses a fixed EdgeDevice
            // Need to figure out a way to create ProtocolGatewayFixture with configurable EdgeDevice
            const int ModulesCount = 2;
            List<TestModule> senders = null;
            List<TestModule> receivers = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            IotHubServiceClient rm = new IotHubServiceClient(edgeDeviceConnectionString);

            try
            {
                senders = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender", ModulesCount, false, clientOptions);
                receivers = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver", ModulesCount, true, clientOptions);

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
                    rm.Dispose();
                }

                if (senders != null)
                {
                    await Task.WhenAll(senders.Select(s => s.Disconnect()));
                }

                if (receivers != null)
                {
                    await Task.WhenAll(receivers.Select(r => r.Disconnect()));
                }
            }

            // wait for the connection to be closed on the Edge side
            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        async Task<List<TestModule>> GetModules(IotHubServiceClient rm, string hostName, string deviceId, string moduleNamePrefix, int count, bool isReceiver, IotHubClientOptions clientOptions)
        {
            var modules = new List<TestModule>();
            for (int i = 1; i <= count; i++)
            {
                string moduleId = moduleNamePrefix + i.ToString();
                TestModule module = await this.GetModule(rm, hostName, deviceId, moduleId, isReceiver, clientOptions);
                modules.Add(module);
            }

            return modules;
        }

        async Task<TestModule> GetModule(IotHubServiceClient rm, string hostName, string deviceId, string moduleId, bool isReceiver, IotHubClientOptions clientOptions)
        {
            string connStr = await RegistryManagerHelper.GetOrCreateModule(rm, hostName, deviceId, moduleId);
            TestModule module = await TestModule.CreateAndConnect(connStr, clientOptions);
            if (isReceiver)
            {
                await module.SetupReceiveMessageHandler();
            }

            return module;
        }
    }
}
