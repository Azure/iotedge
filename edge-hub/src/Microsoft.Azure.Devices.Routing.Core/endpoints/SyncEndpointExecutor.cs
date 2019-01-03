// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;
    using static System.FormattableString;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class SyncEndpointExecutor : IEndpointExecutor
    {
        static readonly RetryStrategy DefaultRetryStrategy = new FixedInterval(0, TimeSpan.FromSeconds(1));
        static readonly TimeSpan DefaultRevivePeriod = TimeSpan.FromHours(1);
        static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        static readonly EndpointExecutorConfig DefaultConfig = new EndpointExecutorConfig(DefaultTimeout, DefaultRetryStrategy, DefaultRevivePeriod, true);

        readonly ICheckpointer checkpointer;
        readonly AtomicBoolean closed;
        readonly CancellationTokenSource cts;
        readonly EndpointExecutorFsm machine;
        readonly AsyncLock sync;

        public Endpoint Endpoint => this.machine.Endpoint;

        public EndpointExecutorStatus Status => this.machine.Status;

        public SyncEndpointExecutor(Endpoint endpoint, ICheckpointer checkpointer)
            : this(endpoint, checkpointer, DefaultConfig)
        {
        }

        public SyncEndpointExecutor(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig config)
        {
            Preconditions.CheckNotNull(endpoint);
            Preconditions.CheckNotNull(config);

            this.checkpointer = Preconditions.CheckNotNull(checkpointer);
            this.machine = new EndpointExecutorFsm(endpoint, checkpointer, config);
            this.cts = new CancellationTokenSource();
            this.closed = new AtomicBoolean();
            this.sync = new AsyncLock();
        }

        public async Task Invoke(IMessage message)
        {
            Events.Invoke(this);

            try
            {
                Preconditions.CheckNotNull(message);

                this.checkpointer.Propose(message);
                if (this.closed)
                {
                    throw new InvalidOperationException($"Endpoint executor for endpoint {this.Endpoint} is closed.");
                }

                // It's only valid that one send message command executes at a time
                using (await this.sync.LockAsync(this.cts.Token))
                {
                    SendMessage command = Commands.SendMessage(message);
                    await this.machine.RunAsync(command);
                    await command.Completion;
                }
                Events.InvokeSuccess(this);
            }
            catch (OperationCanceledException) when (this.cts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Events.InvokeFailure(this, ex);
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

                UpdateEndpoint command = Commands.UpdateEndpoint(newEndpoint);
                await this.machine.RunAsync(command);
                Events.SetEndpointSuccess(this);
            }
            catch (Exception ex)
            {
                Events.SetEndpointFailure(this, ex);
                throw;
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
                    await this.machine.RunAsync(Commands.Close);
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
                this.cts.Dispose();
                this.machine.Dispose();
                this.sync.Dispose();
            }
        }

        static class Events
        {
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<SyncEndpointExecutor>();
            const int IdStart = Routing.EventIds.SyncEndpointExecutor;

            enum EventIds
            {
                Invoke = IdStart,
                InvokeSuccess,
                InvokeFailure,
                SetEndpoint,
                SetEndpointSuccess,
                SetEndpointFailure,
                Close,
                CloseSuccess,
                CloseFailure,
            }

            public static void Invoke(SyncEndpointExecutor executor)
            {
                Log.LogDebug((int)EventIds.Invoke, "[Invoke] Invoke began." + GetContextString(executor.Endpoint));
            }

            public static void InvokeSuccess(SyncEndpointExecutor executor)
            {
                Log.LogDebug((int)EventIds.InvokeSuccess, "[InvokeSuccess] Invoke succeeded." + GetContextString(executor.Endpoint));
            }

            public static void InvokeFailure(SyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.InvokeFailure, ex, "[InvokeFailure] Invoke failed." + GetContextString(executor.Endpoint));
            }

            public static void SetEndpoint(SyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.SetEndpoint, "[SetEndpoint] Set endpoint began." + GetContextString(executor.Endpoint));
            }

            public static void SetEndpointSuccess(SyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.SetEndpointSuccess, "[SetEndpointSuccess] Set endpoint succeeded." + GetContextString(executor.Endpoint));
            }

            public static void SetEndpointFailure(SyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.SetEndpointFailure, ex, "[SetEndpointFailure] Set endpoint failed." + GetContextString(executor.Endpoint));
            }

            public static void Close(SyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.Close, "[Close] Close began." + GetContextString(executor.Endpoint));
            }

            public static void CloseSuccess(SyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.CloseSuccess, "[CloseSuccess] Close succeeded." + GetContextString(executor.Endpoint));
            }

            public static void CloseFailure(SyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.CloseFailure, ex, "[CloseFailure] Close failed." + GetContextString(executor.Endpoint));
            }

            static string GetContextString(Endpoint endpoint)
            {
                return Invariant($" EndpointId: {endpoint.Id}, EndpointName: {endpoint.Name}");
            }
        }
    }
}
