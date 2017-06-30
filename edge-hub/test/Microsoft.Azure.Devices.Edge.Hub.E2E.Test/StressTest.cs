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
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Bvt]
    public class StressTest : IClassFixture<ProtocolHeadFixture>
    {
        readonly IList<string> routes = new List<string>() {
            "FROM /messages/modules/senderA INTO BrokeredEndpoint(\"/modules/receiverA/inputs/input1\")",
            "FROM /messages/modules/senderB INTO BrokeredEndpoint(\"/modules/receiverA/inputs/input1\")",
            "FROM /messages/modules/sender1 INTO BrokeredEndpoint(\"/modules/receiver1/inputs/input1\")",
            "FROM /messages/modules/sender2 INTO BrokeredEndpoint(\"/modules/receiver2/inputs/input1\")",
            "FROM /messages/modules/sender3 INTO BrokeredEndpoint(\"/modules/receiver3/inputs/input1\")",
            "FROM /messages/modules/sender4 INTO BrokeredEndpoint(\"/modules/receiver4/inputs/input1\")",
            "FROM /messages/modules/sender5 INTO BrokeredEndpoint(\"/modules/receiver5/inputs/input1\")",
            "FROM /messages/modules/sender6 INTO BrokeredEndpoint(\"/modules/receiver6/inputs/input1\")",
            "FROM /messages/modules/sender7 INTO BrokeredEndpoint(\"/modules/receiver7/inputs/input1\")",
            "FROM /messages/modules/sender8 INTO BrokeredEndpoint(\"/modules/receiver8/inputs/input1\")",
            "FROM /messages/modules/sender9 INTO BrokeredEndpoint(\"/modules/receiver9/inputs/input1\")",
            "FROM /messages/modules/sender10 INTO BrokeredEndpoint(\"/modules/receiver10/inputs/input1\")"
        };

        string edgeDeviceConnectionString;

        public StressTest(ProtocolHeadFixture protocolHeadFixture)
        {
            protocolHeadFixture.StartMqttHead(routes).Wait();
        }

        [Fact]
        public async Task SingleSenderSingleReceiverTest()
        {
            int messagesCount = 10000;
            Module sender = await this.GetModule("sender1", false);
            Module receiver = await this.GetModule("receiver1", true);
            await receiver.SetupReceiveMessageHandler();

            var task1 = sender.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(2));

            int sentMessagesCount = await task1;
            Assert.Equal(messagesCount, sentMessagesCount);

            await Task.Delay(TimeSpan.FromSeconds(20));
            var receivedMessages = receiver.GetReceivedMessageIndices();
            Assert.Equal(messagesCount, receivedMessages.Count);
        }

        [Fact]
        public async Task MultipleSendersSingleReceiverTest()
        {
            int messagesCount = 5000;
            var sender1 = await this.GetModule("senderA", false);
            var sender2 = await this.GetModule("senderB", false);

            var receiver = await this.GetModule("receiverA", true);

            var task1 = sender1.SendMessagesByCountAsync("output1", 0, messagesCount, TimeSpan.FromMinutes(8));
            var task2 = sender2.SendMessagesByCountAsync("output1", messagesCount, messagesCount, TimeSpan.FromMinutes(8));

            int[] results = await Task.WhenAll(task1, task2);
            int sentMessagesCount = results.Sum();
            Assert.Equal(messagesCount * 2, sentMessagesCount);

            await Task.Delay(TimeSpan.FromSeconds(20));
            var receivedMessages = receiver.GetReceivedMessageIndices();
            Assert.Equal(sentMessagesCount, receivedMessages.Count);
        }

        [Fact]
        public async Task MultipleSendersMultipleReceivers_Count_Test()
        {
            int messagesCount = 5000;

            List<Module> senders = await this.GetModules("sender", 10, false);
            List<Module> receivers = await this.GetModules("receiver", 10, true);
            
            TimeSpan timeout = TimeSpan.FromMinutes(2);
            IEnumerable<Task<int>> tasks = senders.Select(s => s.SendMessagesByCountAsync("output1", 0, messagesCount, timeout));

            int[] results = await Task.WhenAll(tasks);
            int sentMessagesCount = results.Sum();
            Assert.Equal(messagesCount * 10, sentMessagesCount);

            await Task.Delay(TimeSpan.FromSeconds(20));
            int receivedMessagesCount = 0;
            receivers.ForEach(r => receivedMessagesCount += r.GetReceivedMessageIndices().Count);
            Assert.Equal(sentMessagesCount, receivedMessagesCount);
        }

        [Fact]
        public async Task MultipleSendersMultipleReceivers_Duration_Test()
        {
            List<Module> senders = await this.GetModules("sender", 10, false);
            List<Module> receivers = await this.GetModules("receiver", 10, true);

            var sendDuration = TimeSpan.FromMinutes(2);
            IEnumerable<Task<int>> tasks = senders.Select(s => s.SendMessagesForDurationAsync("output1", sendDuration));

            int[] results = await Task.WhenAll(tasks);
            int sentMessagesCount = results.Sum();

            await Task.Delay(TimeSpan.FromSeconds(20));
            int receivedMessagesCount = 0;
            receivers.ForEach(r => receivedMessagesCount += r.GetReceivedMessageIndices().Count);
            Assert.Equal(sentMessagesCount, receivedMessagesCount);
        }

        private async Task<List<Module>> GetModules(string moduleNamePrefix, int count, bool isReceiver)
        {
            var modules = new List<Module>();
            for (int i = 1; i <= count; i++)
            {
                string moduleId = moduleNamePrefix + i.ToString();
                Module module = await this.GetModule(moduleId, isReceiver);
                modules.Add(module);
            }
            return modules;
        }

        private async Task<Module> GetModule(string moduleId, bool isReceiver)
        {
            string connStr = await this.GetModuleConnectionString(moduleId);
            var module = await Module.CreateAndConnect(connStr);
            if (isReceiver)
            {
                await module.SetupReceiveMessageHandler();
            }
            return module;
        }

        private async Task<string> GetModuleConnectionString(string moduleId)
        {
            string edgeDeviceConnectionString = await this.GetEdgeDeviceConnectionString();
            return $"{edgeDeviceConnectionString};GatewayHostName=127.0.0.1;ModuleId={moduleId}";
        }

        private async Task<string> GetEdgeDeviceConnectionString()
        {
            if (this.edgeDeviceConnectionString == null)
            {
                this.edgeDeviceConnectionString = await SecretsHelper.GetSecret("IotEdgeDevice1ConnStr1");
            }
            return this.edgeDeviceConnectionString;
        }

        class Module
        {
            ModuleClient moduleClient;
            readonly Random rand = new Random();
            ISet<int> received;

            Module(ModuleClient moduleClient)
            {
                this.moduleClient = moduleClient;
            }

            public static async Task<Module> CreateAndConnect(string connectionString)
            {
                var settings = new[]
                {
                    new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) { RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true }
                };

                var moduleClient = ModuleClient.CreateFromConnectionString(connectionString, settings);
                await moduleClient.OpenAsync();
                return new Module(moduleClient);
            }

            public Task SetupReceiveMessageHandler()
            {
                this.received = new HashSet<int>();
                return this.moduleClient.SetEventDefaultHandlerAsync(this.MessageHandler, null);
            }

            Task MessageHandler(Message message, object userContext)
            {
                int messageIndex = int.Parse(message.Properties["testId"]);
                this.received.Add(messageIndex);
                return Task.CompletedTask;
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

            private async Task<int> SendMessagesAsync(string output, int startIndex, int count, TimeSpan duration)
            {
                Stopwatch s = new Stopwatch();
                s.Start();
                int i = startIndex;
                for (; i < startIndex + count && s.Elapsed < duration; i++)
                {
                    await this.moduleClient.SendEventAsync(output, this.GetMessage(i.ToString()));
                }

                s.Stop();
                return i - startIndex;
            }

            private Message GetMessage(string id)
            {
                var temp = new Temperature(-10 + rand.Next(40), rand.Next(0, 50) > 40 ? "Invalid" : "F");
                var payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(temp));
                var message = new Message(payloadBytes);
                message.Properties.Add("testId", id);
                message.Properties.Add("Model", "Temperature");
                return message;
            }

            private class Temperature
            {
                public Temperature(double value, string unit)
                {
                    this.Value = value;
                    this.Unit = unit;
                }

                public double Value { get; }
                public string Unit { get; }
            }
        }
    }
}
