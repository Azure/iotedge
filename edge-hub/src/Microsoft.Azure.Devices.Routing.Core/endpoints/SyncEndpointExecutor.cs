// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

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
            Stopwatch stopwatch = Stopwatch.StartNew();
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
                Events.InvokeSuccess(this, stopwatch);
            }
            catch (OperationCanceledException) when (this.cts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Events.InvokeFailure(this, ex, stopwatch);
                throw;
            }
        }

        public async Task SetEndpoint(Endpoint newEndpoint)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
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
                Events.SetEndpointSuccess(this, stopwatch);
            }
            catch (Exception ex)
            {
                Events.SetEndpointFailure(this, ex, stopwatch);
                throw;
            }
        }

        public async Task CloseAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Events.Close(this);

            try
            {
                if (!this.closed.GetAndSet(true))
                {
                    this.cts.Cancel();
                    await this.machine.RunAsync(Commands.Close);
                }
                Events.CloseSuccess(this, stopwatch);
            }
            catch (Exception ex)
            {
                Events.CloseFailure(this, ex, stopwatch);
                throw;
            }
        }

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            Debug.Assert(this.closed);
            if (disposing)
            {
                this.cts.Dispose();
                this.machine.Dispose();
                this.sync.Dispose();
            }
        }

        static class Events
        {
            const string Source = nameof(SyncEndpointExecutor);
            const string DeviceId = null;

            //static readonly ILog Log = Routing.Log;

            public static void Invoke(SyncEndpointExecutor executor)
            {
                //Log.Informational(nameof(Invoke), Source,
                //    "Invoke began." + GetContextString(executor.Endpoint),
                //    executor.Endpoint.IotHubName, DeviceId);
            }

            public static void InvokeSuccess(SyncEndpointExecutor executor, Stopwatch stopwatch)
            {
                //Log.Informational(nameof(InvokeSuccess), Source,
                //    "Invoke succeeded." + GetContextString(executor.Endpoint),
                //    executor.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void InvokeFailure(SyncEndpointExecutor executor, Exception ex, Stopwatch stopwatch)
            {
                //Log.Error(nameof(InvokeFailure), Source,
                //    "Invoke failed." + GetContextString(executor.Endpoint),
                //    ex, executor.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void SetEndpoint(SyncEndpointExecutor executor)
            {
                //Log.Informational(nameof(SetEndpoint), Source,
                //    "Set endpoint began." + GetContextString(executor.Endpoint),
                //    executor.Endpoint.IotHubName, DeviceId);
            }

            public static void SetEndpointSuccess(SyncEndpointExecutor executor, Stopwatch stopwatch)
            {
                //Log.Informational(nameof(SetEndpointSuccess), Source,
                //    "Set endpoint succeeded." + GetContextString(executor.Endpoint),
                //    executor.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void SetEndpointFailure(SyncEndpointExecutor executor, Exception ex, Stopwatch stopwatch)
            {
                //Log.Error(nameof(SetEndpointFailure), Source,
                //    "Set endpoint failed." + GetContextString(executor.Endpoint),
                //    ex, executor.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void Close(SyncEndpointExecutor executor)
            {
                //Log.Informational(nameof(Close), Source,
                //    "Close began." + GetContextString(executor.Endpoint),
                //    executor.Endpoint.IotHubName, DeviceId);
            }

            public static void CloseSuccess(SyncEndpointExecutor executor, Stopwatch stopwatch)
            {
                //Log.Informational(nameof(CloseSuccess), Source,
                //    "Close succeeded." + GetContextString(executor.Endpoint),
                //    executor.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void CloseFailure(SyncEndpointExecutor executor, Exception ex, Stopwatch stopwatch)
            {
                //Log.Error(nameof(CloseFailure), Source,
                //    "Close failed." + GetContextString(executor.Endpoint),
                //    ex, executor.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            static string GetContextString(Endpoint endpoint)
            {
                return string.Format(CultureInfo.InvariantCulture, " EndpointId: {0}, EndpointName: {1}", endpoint.Id, endpoint.Name);
            }
        }
    }
}