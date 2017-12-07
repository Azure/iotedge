namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;

    [Bvt, E2E, Stress]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
    [TestCaseOrderer("Microsoft.Azure.Devices.Edge.Util.Test.PriorityOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
    public class StressTest
    {
        [Fact, TestPriority(301)]
        public async Task SingleSenderSingleReceiverTest()
        {
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_SingleSender"], out int messagesCount);
            Module sender = null;
            Module receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender1", false);
                receiver = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver1", true);

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

        [Fact, TestPriority(302)]
        public async Task MultipleSendersSingleReceiverTest()
        {
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_MultipleSenders"], out int messagesCount);
            Module sender1 = null;
            Module sender2 = null;
            Module receiver = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                sender1 = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "senderA", false);
                sender2 = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "senderB", false);
                receiver = await this.GetModule(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiverA", true);

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

        [Fact, TestPriority(303)]
        public async Task MultipleSendersMultipleReceivers_Count_Test()
        {
            // The modules limit is because ProtocolGatewayFixture currently uses a fixed EdgeDevice
            // Need to figure out a way to create ProtocolGatewayFixture with configurable EdgeDevice
            const int ModulesCount = 2;
            int.TryParse(ConfigHelper.TestConfig["StressTest_MessagesCount_MultipleSendersMultipleReceivers"], out int messagesCount);
            List<Module> senders = null;
            List<Module> receivers = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                senders = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender", ModulesCount, false);
                receivers = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver", ModulesCount, true);

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

        [Fact, TestPriority(304)]
        public async Task MultipleSendersMultipleReceivers_Duration_Test()
        {
            // The modules limit is because ProtocolGatewayFixture currently uses a fixed EdgeDevice
            // Need to figure out a way to create ProtocolGatewayFixture with configurable EdgeDevice
            const int ModulesCount = 2;
            List<Module> senders = null;
            List<Module> receivers = null;

            string edgeDeviceConnectionString = await SecretsHelper.GetSecretFromConfigKey("edgeCapableDeviceConnStrKey");
            IotHubConnectionStringBuilder connectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeDeviceConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(edgeDeviceConnectionString);

            try
            {
                senders = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "sender", ModulesCount, false);
                receivers = await this.GetModules(rm, connectionStringBuilder.HostName, connectionStringBuilder.DeviceId, "receiver", ModulesCount, true);

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

        async Task<List<Module>> GetModules(RegistryManager rm, string hostName, string deviceId, string moduleNamePrefix, int count, bool isReceiver)
        {
            var modules = new List<Module>();
            for (int i = 1; i <= count; i++)
            {
                string moduleId = moduleNamePrefix + i.ToString();
                Module module = await this.GetModule(rm, hostName, deviceId, moduleId, isReceiver);
                modules.Add(module);
            }
            return modules;
        }

        async Task<Module> GetModule(RegistryManager rm, string hostName, string deviceId, string moduleId, bool isReceiver)
        {
            string connStr = await RegistryManagerHelper.GetOrCreateModule(rm, hostName, deviceId, moduleId);
            Module module = await Module.CreateAndConnect(connStr);
            if (isReceiver)
            {
                await module.SetupReceiveMessageHandler();
            }
            return module;
        }

                //string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
                //this.edgeDeviceConnectionString = $"{this.edgeDeviceConnectionString};GatewayHostName={gatewayHostname}";
        class Module
        {
            readonly DeviceClient deviceClient;
            ISet<int> received;

            Module(DeviceClient deviceClient)
            {
                this.deviceClient = deviceClient;
            }

            public static async Task<Module> CreateAndConnect(string connectionString)
            {
                var settings = new[]
                {
                    new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                    {
                        RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                    }
                };

                DeviceClient moduleClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
                await moduleClient.OpenAsync();
                return new Module(moduleClient);
            }

            public Task SetupReceiveMessageHandler()
            {
                this.received = new HashSet<int>();
                return this.deviceClient.SetMessageHandlerAsync(this.MessageHandler, null);
            }

            Task<MessageResponse> MessageHandler(Message message, object userContext)
            {
                int messageIndex = int.Parse(message.Properties["testId"]);
                this.received.Add(messageIndex);
                return Task.FromResult(MessageResponse.Completed);
            }

            public ISet<int> GetReceivedMessageIndices() => this.received;

            public async Task<int> SendMessagesByCountAsync(string output, int startIndex, int count, TimeSpan timeout)
            {
                int sentMessagesCount = await this.SendMessagesAsync(output, startIndex, count, timeout);
                if (sentMessagesCount < count)
                {
                    throw new TimeoutException($"Attempted to send {count} messages in {timeout.TotalSeconds} seconds, but was able to send only {sentMessagesCount}");
                }
                return sentMessagesCount;
            }

            public Task<int> SendMessagesForDurationAsync(string output, TimeSpan duration) => this.SendMessagesAsync(output, 0, int.MaxValue, duration);

            async Task<int> SendMessagesAsync(string output, int startIndex, int count, TimeSpan duration)
            {
                var s = new Stopwatch();
                s.Start();
                int i = startIndex;
                for (; i < startIndex + count && s.Elapsed < duration; i++)
                {
                    await this.deviceClient.SendEventAsync(output, this.GetMessage(i.ToString()));
                }

                s.Stop();
                return i - startIndex;
            }

            Message GetMessage(string id)
            {
                var temp = new Temperature();
                byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(temp));
                var message = new Message(payloadBytes);
                message.Properties.Add("testId", id);
                message.Properties.Add("Model", "Temperature");
                return message;
            }

            public Task Disconnect() => this.deviceClient.CloseAsync();

            class Temperature
            {
            }
        }
    }
}
