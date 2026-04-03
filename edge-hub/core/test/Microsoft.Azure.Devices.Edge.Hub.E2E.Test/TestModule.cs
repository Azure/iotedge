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
        readonly IotHubModuleClient moduleClient;
        readonly IDictionary<string, ISet<int>> receivedForInput;
        IList<DirectMethodRequest> receivedMethodRequests;
        bool disposed = false;

        TestModule(IotHubModuleClient moduleClient)
        {
            this.moduleClient = moduleClient;
            this.receivedForInput = new Dictionary<string, ISet<int>>();
        }

        ~TestModule()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.moduleClient?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            this.disposed = true;
        }

        public static async Task<TestModule> CreateAndConnect(string connectionString, IotHubClientOptions options, int retryCount = int.MaxValue)
        {
            IotHubModuleClient moduleClient = IotHubModuleClient.CreateFromConnectionString(connectionString, options);
            IRetryPolicy defaultRetryStrategy = new ExponentialBackoff(
                retryCount: retryCount,
                minBackoff: TimeSpan.FromMilliseconds(100),
                maxBackoff: TimeSpan.FromSeconds(10),
                deltaBackoff: TimeSpan.FromMilliseconds(100));

            moduleClient.SetRetryPolicy(defaultRetryStrategy);
            await moduleClient.OpenAsync();
            return new TestModule(moduleClient);
        }

        public static async Task<TestModule> CreateAndConnect(IotHubServiceClient rm, string hostName, string deviceId, string moduleId, IotHubClientOptions options, int retryCount = int.MaxValue)
        {
            string connStr = await RegistryManagerHelper.GetOrCreateModule(rm, hostName, deviceId, moduleId);
            return await CreateAndConnect(connStr, options, retryCount);
        }

        public Task SetupReceiveMessageHandler()
        {
            this.receivedForInput["_"] = new HashSet<int>();
            return this.moduleClient.SetIncomingMessageCallbackAsync(this.MessageHandler);
        }

        public Task SetupReceiveMessageHandler(string input)
        {
            this.receivedForInput[input] = new HashSet<int>();
            return this.moduleClient.SetIncomingMessageCallbackAsync(this.MessageHandler);
        }

        public ISet<int> GetReceivedMessageIndices() => this.receivedForInput["_"];

        public ISet<int> GetReceivedMessageIndices(string input) => this.receivedForInput[input];

        public Task<int> SendMessagesByCountAndSizeAsync(string output, int startIndex, int count, int size, TimeSpan sleepTime, TimeSpan timeout) =>
            this.SendMessagesAsync(output, startIndex, count, timeout, sleepTime, size);

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

        public Task SendMessageAsync(string output, TelemetryMessage message)
        {
            return this.moduleClient.SendTelemetryAsync(output, message);
        }

        public void SetupReceiveMethodHandler(string methodName = null, Func<DirectMethodRequest, Task<DirectMethodResponse>> callback = null)
        {
            this.receivedMethodRequests = new List<DirectMethodRequest>();
            Func<DirectMethodRequest, Task<DirectMethodResponse>> methodCallback = callback ?? this.DefaultMethodCallback;
            if (!string.IsNullOrWhiteSpace(methodName))
            {
                // v2 SDK only supports a single global callback; filter by method name inside the wrapper
                var innerCallback = methodCallback;
                methodCallback = (request) =>
                {
                    if (string.Equals(request.MethodName, methodName, StringComparison.OrdinalIgnoreCase))
                    {
                        return innerCallback(request);
                    }

                    return Task.FromResult(new DirectMethodResponse(404));
                };
            }

            this.moduleClient.SetDirectMethodCallbackAsync(methodCallback);
        }

        public Task Disconnect() => this.moduleClient.CloseAsync();

        Task<MessageAcknowledgement> MessageHandler(IncomingMessage message)
        {
            int messageIndex = int.Parse(message.Properties["testId"]);
            string inputName = message.InputName ?? "_";
            if (this.receivedForInput.TryGetValue(inputName, out ISet<int> received))
            {
                received.Add(messageIndex);
            }
            else if (this.receivedForInput.TryGetValue("_", out ISet<int> defaultReceived))
            {
                defaultReceived.Add(messageIndex);
            }

            return Task.FromResult(MessageAcknowledgement.Complete);
        }

        async Task<int> SendMessagesAsync(string output, int startIndex, int count, TimeSpan duration, TimeSpan sleepTime, int? size = null)
        {
            var s = new Stopwatch();
            s.Start();
            int i = startIndex;
            for (; i < startIndex + count && s.Elapsed < duration; i++)
            {
                await this.moduleClient.SendTelemetryAsync(output, size.HasValue ? this.GetMessageWithSize(i.ToString(), size.Value) : this.GetMessage(i.ToString()));
                await Task.Delay(sleepTime);
            }

            s.Stop();
            return i - startIndex;
        }

        Task<DirectMethodResponse> DefaultMethodCallback(DirectMethodRequest methodRequest)
        {
            this.receivedMethodRequests.Add(methodRequest);
            return Task.FromResult(new DirectMethodResponse(200));
        }

        TelemetryMessage GetMessage(string id)
        {
            var temp = new Temperature();
            byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(temp));
            var message = new TelemetryMessage(payloadBytes);
            message.Properties.Add("testId", id);
            message.Properties.Add("Model", "Temperature");
            return message;
        }

        TelemetryMessage GetMessageWithSize(string id, int size)
        {
            var random = new Random();
            byte[] data = new byte[size];
            random.NextBytes(data);
            var messageBody = new { data = data };
            byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody));
            var message = new TelemetryMessage(payloadBytes);
            message.Properties.Add("testId", id);
            message.Properties.Add("Model", "Binary");
            return message;
        }

        class Temperature
        {
        }
    }
}
