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
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class StoringAsyncEndpointExecutor : AsyncEndpointExecutor
    {
        const int BatchSize = 10;
        static readonly TimeSpan WaitForMessagesTimeout = TimeSpan.FromSeconds(15);

        readonly IMessageStore messageStore;
        readonly Task sendMessageTask;
        readonly ManualResetEvent hasMessagesInQueue = new ManualResetEvent(true);

        public StoringAsyncEndpointExecutor(Endpoint endpoint,
            ICheckpointer checkpointer,
            EndpointExecutorConfig config,
            AsyncEndpointExecutorOptions options,
            IMessageStore messageStore)
            : base(endpoint, checkpointer, config, options)
        {
            this.messageStore = messageStore;
            this.sendMessageTask = Task.Run(this.SendMessagesPump);
        }

        public override async Task Invoke(IMessage message)
        {
            try
            {
                long offset = await this.messageStore.Add(this.Endpoint.Id, message);
                Events.AddMessageSuccess(this, offset);
                this.hasMessagesInQueue.Set();
            }
            catch(Exception ex)
            {
                Events.AddMessageFailure(this, ex);
                throw;
            }
        }

        private async Task SendMessagesPump()
        {
            try
            {
                Events.StartSendMessagesPump(this);
                IMessageIterator iterator = this.messageStore.GetMessageIterator(this.Endpoint.Id, this.Checkpointer.Offset + 1);
                while (!this.CancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        if (this.hasMessagesInQueue.WaitOne(WaitForMessagesTimeout))
                        {
                            this.hasMessagesInQueue.Reset();
                            IEnumerable<IMessage> messages = await iterator.GetNext(BatchSize);
                            IEnumerable<IMessage> messagesAsList = messages as IList<IMessage> ?? messages.ToList();
                            foreach (IMessage message in messagesAsList)
                            {
                                await this.SendToTplHead(message);
                            }
                            Events.SendMessagesSuccess(this, messagesAsList);
                        }
                    }
                    catch (Exception ex)
                    {
                        Events.SendMessagesError(this, ex);
                        // Swallow exception and keep trying.
                    }
                }
            }
            catch(Exception ex)
            {
                Events.SendMessagesPumpFailure(this, ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this.messageStore?.Dispose();
            }
        }

        public override async Task CloseAsync()
        {
            await base.CloseAsync();
            if (this.messageStore != null)
                await this.messageStore?.RemoveEndpoint(this.Endpoint.Id);
            await (this.sendMessageTask ?? Task.CompletedTask);            
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
                SendMessagesSuccess,
                SendMessagesPumpFailure
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

            public static void SendMessagesSuccess(StoringAsyncEndpointExecutor executor, IEnumerable<IMessage> messages)
            {
                Log.LogDebug((int)EventIds.SendMessagesSuccess, Invariant($"[SendMessagesSuccess] Successfully sent {messages.Count()} messages to IPL head for EndpointId: {executor.Endpoint.Id}."));
            }

            public static void SendMessagesPumpFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogCritical((int)EventIds.SendMessagesPumpFailure, ex, $"[SendMessagesPumpFailure] Unable to start pump to send stored messages for EndpointId: {executor.Endpoint.Id}.");
            }
        }
    }
}
