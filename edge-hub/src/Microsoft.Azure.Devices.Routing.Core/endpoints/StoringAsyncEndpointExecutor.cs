// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class StoringAsyncEndpointExecutor : IEndpointExecutor
    {
        readonly AtomicBoolean closed = new AtomicBoolean();
        readonly IMessageStore messageStore;
        readonly Task sendMessageTask;
        readonly ManualResetEvent hasMessagesInQueue = new ManualResetEvent(true);
        readonly ICheckpointer checkpointer;
        readonly AsyncEndpointExecutorOptions options;
        readonly EndpointExecutorFsm machine;
        readonly CancellationTokenSource cts = new CancellationTokenSource();

        public Endpoint Endpoint => this.machine.Endpoint;

        public EndpointExecutorStatus Status => this.machine.Status;

        public StoringAsyncEndpointExecutor(Endpoint endpoint,
            ICheckpointer checkpointer,
            EndpointExecutorConfig config,
            AsyncEndpointExecutorOptions options,
            IMessageStore messageStore)
        {
            Preconditions.CheckNotNull(endpoint);
            Preconditions.CheckNotNull(config);
            this.checkpointer = Preconditions.CheckNotNull(checkpointer);
            this.options = Preconditions.CheckNotNull(options);
            this.machine = new EndpointExecutorFsm(endpoint, checkpointer, config);
            this.messageStore = messageStore;
            this.sendMessageTask = Task.Run(this.SendMessagesPump);
        }

        public async Task Invoke(IMessage message)
        {
            try
            {
                if (this.closed)
                {
                    throw new InvalidOperationException($"Endpoint executor for endpoint {this.Endpoint} is closed.");
                }
                long offset = await this.messageStore.Add(this.Endpoint.Id, message);
                this.checkpointer.Propose(message);
                Events.AddMessageSuccess(this, offset);
                this.hasMessagesInQueue.Set();
            }
            catch (Exception ex)
            {
                Events.AddMessageFailure(this, ex);
                throw;
            }
        }

        async Task SendMessagesPump()
        {
            try
            {
                Events.StartSendMessagesPump(this);
                IMessageIterator iterator = this.messageStore.GetMessageIterator(this.Endpoint.Id, this.checkpointer.Offset + 1);
                while (!this.cts.IsCancellationRequested)
                {
                    try
                    {
                        this.hasMessagesInQueue.WaitOne(this.options.BatchTimeout);
                        IMessage[] messages = (await iterator.GetNext(this.options.BatchSize)).ToArray();
                        await this.ProcessMessages(messages);
                        Events.SendMessagesSuccess(this, messages);

                        // If store has no messages, then reset the hasMessagesInQueue flag. 
                        if (messages.Length == 0)
                        {
                            this.hasMessagesInQueue.Reset();
                        }
                    }
                    catch (Exception ex)
                    {
                        Events.SendMessagesError(this, ex);
                        // Swallow exception and keep trying.
                    }
                }
            }
            catch (Exception ex)
            {
                Events.SendMessagesPumpFailure(this, ex);
            }
        }

        async Task ProcessMessages(IMessage[] messages)
        {
            Events.ProcessingMessages(this, messages);
            SendMessage command = Commands.SendMessage(messages);
            await this.machine.RunAsync(command);
            await command.Completion;
        }

        public void Dispose() => this.Dispose(true);

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.cts.Dispose();
                this.machine.Dispose();
            }
        }

        public async Task CloseAsync()
        {
            Events.Close(this);

            try
            {
                if (!this.closed.GetAndSet(true))
                {
                    this.cts.Cancel();
                    await (this.messageStore?.RemoveEndpoint(this.Endpoint.Id) ?? Task.CompletedTask);
                    await (this.sendMessageTask ?? Task.CompletedTask);
                }
                Events.CloseSuccess(this);
            }
            catch (Exception ex)
            {
                Events.CloseFailure(this, ex);
                throw;
            }
        }

        public async Task SetEndpoint(Endpoint newEndpoint)
        {
            Events.SetEndpoint(this);

            try
            {
                Preconditions.CheckNotNull(newEndpoint);
                Preconditions.CheckArgument(newEndpoint.Id.Equals(this.Endpoint.Id), $"Can only set new endpoint with same id. Given {newEndpoint.Id}, expected {this.Endpoint.Id}");

                if (this.closed)
                {
                    throw new InvalidOperationException($"Endpoint executor for endpoint {this.Endpoint} is closed.");
                }
                await this.machine.RunAsync(Commands.UpdateEndpoint(newEndpoint));
                Events.SetEndpointSuccess(this);
            }
            catch (Exception ex)
            {
                Events.SetEndpointFailure(this, ex);
                throw;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<StoringAsyncEndpointExecutor>();
            const int IdStart = Routing.EventIds.StoringAsyncEndpointExecutor;

            enum EventIds
            {
                AddMessageSuccess = IdStart,
                StartSendMessagesPump,
                SendMessagesError,
                ProcessMessagesSuccess,
                SendMessagesPumpFailure,
                ProcessingMessages,
                SetEndpoint,
                SetEndpointSuccess,
                SetEndpointFailure,
                Close,
                CloseSuccess,
                CloseFailure,
            }

            public static void AddMessageSuccess(StoringAsyncEndpointExecutor executor, long offset)
            {
                Log.LogDebug((int)EventIds.AddMessageSuccess, $"[AddMessageSuccess] Successfully added message to store for EndpointId: {executor.Endpoint.Id}, Message offset: {offset}");
            }

            public static void AddMessageFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.AddMessageSuccess, ex, $"[AddMessageFailure] Error adding added message to store for EndpointId: {executor.Endpoint.Id}");
            }

            public static void StartSendMessagesPump(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.StartSendMessagesPump, $"[StartSendMessagesPump] Starting pump to send stored messages to EndpointId: {executor.Endpoint.Id}.");
            }

            public static void SendMessagesError(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogWarning((int)EventIds.SendMessagesError, ex, $"[SendMessageError] Error sending message batch to endpoint to IPL head for EndpointId: {executor.Endpoint.Id}.");
            }

            public static void SendMessagesSuccess(StoringAsyncEndpointExecutor executor, ICollection<IMessage> messages)
            {
                if (messages.Count > 0)
                {
                    Log.LogDebug((int)EventIds.ProcessMessagesSuccess, Invariant($"[ProcessMessagesSuccess] Successfully processed {messages.Count} messages for EndpointId: {executor.Endpoint.Id}."));
                }
            }

            public static void ProcessingMessages(StoringAsyncEndpointExecutor executor, ICollection<IMessage> messages)
            {
                if (messages.Count > 0)
                {
                    Log.LogDebug((int)EventIds.ProcessingMessages, Invariant($"[ProcessingMessages] Processing {messages.Count} messages for EndpointId: {executor.Endpoint.Id}."));
                }
            }

            public static void SendMessagesPumpFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogCritical((int)EventIds.SendMessagesPumpFailure, ex, $"[SendMessagesPumpFailure] Unable to start pump to send stored messages for EndpointId: {executor.Endpoint.Id}.");
            }

            public static void SetEndpoint(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.SetEndpoint, "[SetEndpoint] Set endpoint began. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void SetEndpointSuccess(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.SetEndpointSuccess, "[SetEndpointSuccess] Set endpoint succeeded. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void SetEndpointFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.SetEndpointFailure, ex, "[SetEndpointFailure] Set endpoint failed. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void Close(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.Close, "[Close] Close began. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void CloseSuccess(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.CloseSuccess, "[CloseSuccess] Close succeeded. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void CloseFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.CloseFailure, ex, "[CloseFailure] Close failed. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }
        }
    }
}
