// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    public class TestModule
    {
        readonly ModuleClient moduleClient;
        readonly IDictionary<string, ISet<int>> receivedForInput;
        IList<MethodRequest> receivedMethodRequests;

        TestModule(ModuleClient moduleClient)
        {
            this.moduleClient = moduleClient;
            this.receivedForInput = new Dictionary<string, ISet<int>>();
        }

        public static async Task<TestModule> CreateAndConnect(string connectionString, ITransportSettings[] settings)
        {
            ModuleClient moduleClient = ModuleClient.CreateFromConnectionString(connectionString, settings);
            await moduleClient.OpenAsync();
            return new TestModule(moduleClient);
        }

        public static async Task<TestModule> CreateAndConnect(RegistryManager rm, string hostName, string deviceId, string moduleId, ITransportSettings[] transportSettings)
        {
            string connStr = await RegistryManagerHelper.GetOrCreateModule(rm, hostName, deviceId, moduleId);
            return await CreateAndConnect(connStr, transportSettings);
        }

        public Task SetupReceiveMessageHandler()
        {
            this.receivedForInput["_"] = new HashSet<int>();
            return this.moduleClient.SetMessageHandlerAsync(this.MessageHandler, this.receivedForInput["_"]);
        }

        public Task SetupReceiveMessageHandler(string input)
        {
            this.receivedForInput[input] = new HashSet<int>();
            return this.moduleClient.SetInputMessageHandlerAsync(input, this.MessageHandler, this.receivedForInput[input]);
        }

        public ISet<int> GetReceivedMessageIndices() => this.receivedForInput["_"];

        public ISet<int> GetReceivedMessageIndices(string input) => this.receivedForInput[input];

        public Task<int> SendMessagesByCountAsync(string output, int startIndex, int count, TimeSpan timeout) =>
            this.SendMessagesByCountAsync(output, startIndex, count, timeout, TimeSpan.Zero);

        public async Task<int> SendMessagesByCountAsync(string output, int startIndex, int count, TimeSpan timeout, TimeSpan sleepTime)
        {
            int sentMessagesCount = await this.SendMessagesAsync(output, startIndex, count, timeout, sleepTime);
            if (sentMessagesCount < count)
            {
                throw new TimeoutException($"Attempted to send {count} messages in {timeout.TotalSeconds} seconds, but was able to send only {sentMessagesCount}");
            }

            return sentMessagesCount;
        }

        public Task<int> SendMessagesForDurationAsync(string output, TimeSpan duration) => this.SendMessagesAsync(output, 0, int.MaxValue, duration, TimeSpan.Zero);

        public Task SendMessageAsync(string output, Message message)
        {
            return this.moduleClient.SendEventAsync(output, message);
        }

        public void SetupReceiveMethodHandler(string methodName = null, MethodCallback callback = null)
        {
            this.receivedMethodRequests = new List<MethodRequest>();
            MethodCallback methodCallback = callback ?? this.DefaultMethodCallback;
            if (string.IsNullOrWhiteSpace(methodName))
            {
                this.moduleClient.SetMethodDefaultHandlerAsync(methodCallback, null);
            }
            else
            {
                this.moduleClient.SetMethodHandlerAsync(methodName, methodCallback, null);
            }
        }

        public Task Disconnect() => this.moduleClient.CloseAsync();

        Task<MessageResponse> MessageHandler(Message message, object userContext)
        {
            int messageIndex = int.Parse(message.Properties["testId"]);
            var received = userContext as ISet<int>;
            received?.Add(messageIndex);
            return Task.FromResult(MessageResponse.Completed);
        }

        async Task<int> SendMessagesAsync(string output, int startIndex, int count, TimeSpan duration, TimeSpan sleepTime)
        {
            var s = new Stopwatch();
            s.Start();
            int i = startIndex;
            for (; i < startIndex + count && s.Elapsed < duration; i++)
            {
                await this.moduleClient.SendEventAsync(output, this.GetMessage(i.ToString()));
                await Task.Delay(sleepTime);
            }

            s.Stop();
            return i - startIndex;
        }

        Task<MethodResponse> DefaultMethodCallback(MethodRequest methodRequest, object context)
        {
            this.receivedMethodRequests.Add(methodRequest);
            return Task.FromResult(new MethodResponse(200));
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

        class Temperature
        {
        }
    }
}
