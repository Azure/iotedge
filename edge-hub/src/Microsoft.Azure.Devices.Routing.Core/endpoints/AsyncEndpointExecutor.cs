// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Delivers messages to endpoints asynchronously.
    /// <br/>
    /// This is implemented using TPL DataFlow to handle the asynchronicity, with the following pipeline:
    /// <br/>
    /// <pre>
    ///                   Message   +--------+  Message   +-------+  Message[]   +-----------------+
    ///  Invoke(message) ---------> | Buffer | ---------> | Batch | -----------> | ProcessMessages |
    ///                             +--------+            +-------+              +-----------------+
    /// </pre>
    /// The invoke method on this executor returns after the message has been successfully inserted into
    /// the first buffer block. A batch block handles grouping of messages to reduce the number of requests
    /// made to the endpoint and increase throughput. This block emits a batch when the batch size is reached or
    /// the batch timer times out.
    /// </summary>
    public class AsyncEndpointExecutor : IEndpointExecutor
    {
        const int MaxMessagesPerTask = 1000;

        readonly Timer batchTimer;        
        readonly AtomicBoolean closed;
        readonly CancellationTokenSource cts;
        readonly ITargetBlock<IMessage> head;
        readonly EndpointExecutorFsm machine;
        readonly AsyncEndpointExecutorOptions options;
        readonly IDataflowBlock tail;

        public Endpoint Endpoint => this.machine.Endpoint;

        public EndpointExecutorStatus Status => this.machine.Status;

        public AsyncEndpointExecutor(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig config, AsyncEndpointExecutorOptions options)
        {
            Preconditions.CheckNotNull(endpoint);
            Preconditions.CheckNotNull(config);
            this.Checkpointer = Preconditions.CheckNotNull(checkpointer);
            this.cts = new CancellationTokenSource();
            this.options = Preconditions.CheckNotNull(options);
            this.machine = new EndpointExecutorFsm(endpoint, checkpointer, config);
            this.closed = new AtomicBoolean();

            // The three size variables below adjust the following parameters:
            //    1. MaxMessagesPerTask - the maximum number of messages the batch block will process before yielding
            //    2. BoundedCapacity - the size of the batch blocks input buffer
            //    3. BatchBlock ctor - the maximum size of each batch emitted by the block (can be smaller because of the timer)
            var batchOptions = new GroupingDataflowBlockOptions
            {
                MaxMessagesPerTask = MaxMessagesPerTask,
                BoundedCapacity = options.BatchSize
            };
            var batchBlock = new BatchBlock<IMessage>(options.BatchSize, batchOptions);
            this.batchTimer = new Timer(Trigger, batchBlock, options.BatchTimeout, options.BatchTimeout);

            var processOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 1
            };
            var process = new ActionBlock<IMessage[]>(this.MessagesAction, processOptions);

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            batchBlock.LinkTo(process, linkOptions);

            this.head = batchBlock;
            this.tail = process;
        }

        protected ICheckpointer Checkpointer { get; }

        protected CancellationTokenSource CancellationTokenSource => this.cts;

        public virtual Task Invoke(IMessage message) => this.SendToTplHead(message);

        protected async Task SendToTplHead(IMessage message)
        {
            try
            {
                Preconditions.CheckNotNull(message);

                this.Checkpointer.Propose(message);

                if (this.closed)
                {
                    throw new InvalidOperationException($"Endpoint executor for endpoint {this.Endpoint} is closed.");
                }
                await this.head.SendAsync(message, this.cts.Token);
                Events.InvokeSuccess(this, message);
            }
            catch (Exception ex)
            {
                if (this.closed)
                {
                    Events.InvokeWarning(this, ex, message);
                }
                else
                {
                    Events.InvokeFailure(this, ex, message);
                    throw;
                }
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

        public virtual async Task CloseAsync()
        {
            Events.Close(this);

            try
            {
                if (!this.closed.GetAndSet(true))
                {
                    this.cts.Cancel();
                    this.head.Complete();
                    await Task.WhenAll(this.tail.Completion, this.machine.RunAsync(Commands.Close));
                }
                Events.CloseSuccess(this);
            }
            catch (Exception ex)
            {
                Events.CloseFailure(this, ex);
                throw;
            }
        }

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            //Debug.Assert(this.closed);
            if (disposing)
            {
                this.batchTimer.Dispose();
                this.cts.Dispose();
                this.machine.Dispose();
            }
        }

        async Task MessagesAction(IMessage[] messages)
        {
            try
            {
                // Disable the timer while processing the batch
                this.batchTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                Events.ProcessMessages(this, messages);

                SendMessage command = Commands.SendMessage(messages);
                await this.machine.RunAsync(command);
                await command.Completion;

                if (!this.closed)
                {
                    this.batchTimer.Change(this.options.BatchTimeout, this.options.BatchTimeout);
                }
                Events.ProcessMessagesSuccess(this, messages);
            }
            catch (Exception ex)
            {
                Events.ProcessMessagesFailure(this, messages, ex);
                throw;
            }
        }

        /// <summary>
        /// Batch trigger timer callback. This callback is disabled when processing a group of messages
        /// and re-enabled after processing for the batch has completed. It is harmless if TriggerBatch
        /// is called multiple times (in the worst case several extra batches will be generated, but they
        /// will all be processed correctly).
        /// </summary>
        /// <param name="block"></param>
        static void Trigger(object block)
        {
            ((BatchBlock<IMessage>)block).TriggerBatch();
        }

        static class Events
        {
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<AsyncEndpointExecutor>();
            const int IdStart = Routing.EventIds.AsyncEndpointExecutor;

            enum EventIds
            {
                InvokeSuccess = IdStart,
                InvokeFailure,
                InvokeWarning,
                ProcessMessages,
                ProcessMessagesSuccess,
                ProcessMessagesFailure,
                SetEndpoint,
                SetEndpointSuccess,
                SetEndpointFailure,
                Close,
                CloseSuccess,
                CloseFailure,
            }

            public static void InvokeSuccess(AsyncEndpointExecutor executor, IMessage message)
            {
                Log.LogDebug((int)EventIds.InvokeSuccess, "[InvokeSuccess] Invoke succeeded. EndpointId: {0}, EndpointName: {1}, Offset:{2}", executor.Endpoint.Id, executor.Endpoint.Name, message?.Offset);
            }

            public static void InvokeFailure(AsyncEndpointExecutor executor, Exception ex, IMessage message)
            {
                Log.LogCritical((int)EventIds.InvokeFailure, ex, "[InvokeFailure] Invoke failed. EndpointId: {0}, EndpointName: {1}, Offset: {2}", executor.Endpoint.Id, executor.Endpoint.Name, message?.Offset);
            }

            public static void InvokeWarning(AsyncEndpointExecutor executor, Exception ex, IMessage message)
            {
                Log.LogWarning((int)EventIds.InvokeWarning, ex, "[InvokeWarning] Invoke failed. EndpointId: {0}, EndpointName: {1}, Offset: {2}",
                    executor.Endpoint.Id, executor.Endpoint.Name, message?.Offset);
            }

            public static void ProcessMessages(AsyncEndpointExecutor executor, ICollection<IMessage> messages)
            {
                Log.LogDebug((int)EventIds.ProcessMessages, "[ProcessMessages] Process messages began. EndpointId: {0}, EndpointName: {1}, BatchSize: {2}", executor.Endpoint.Id, executor.Endpoint.Name, messages.Count);
            }

            public static void ProcessMessagesSuccess(AsyncEndpointExecutor executor, ICollection<IMessage> messages)
            {
                Log.LogDebug((int)EventIds.ProcessMessagesSuccess, "[ProcessMessagesSuccess] Process messages succeeded. EndpointId: {0}, EndpointName: {1}, BatchSize: {2}", executor.Endpoint.Id, executor.Endpoint.Name, messages.Count);
            }

            public static void ProcessMessagesFailure(AsyncEndpointExecutor executor, IMessage[] messages, Exception ex)
            {
                Log.LogCritical((int)EventIds.ProcessMessagesFailure, ex, "[ProcessMessagesFailure] Process messages failed. EndpointId: {0}, EndpointName: {1}, BatchSize: {2}", executor.Endpoint.Id, executor.Endpoint.Name, messages.Length);
            }

            public static void SetEndpoint(AsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.SetEndpoint, "[SetEndpoint] Set endpoint began. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void SetEndpointSuccess(AsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.SetEndpointSuccess, "[SetEndpointSuccess] Set endpoint succeeded. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void SetEndpointFailure(AsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.SetEndpointFailure, ex, "[SetEndpointFailure] Set endpoint failed. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void Close(AsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.Close, "[Close] Close began. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void CloseSuccess(AsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.CloseSuccess, "[CloseSuccess] Close succeeded. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void CloseFailure(AsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.CloseFailure, ex, "[CloseFailure] Close failed. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }
        }
    }
}
