// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;

    public class FrontendRoutingService : IRoutingService
    {
        const string PerfCounterOperationName = "FrontendRoutingService.RouteAsync";
        const string PerfCounterOperationSuccessStatus = "success";
        const string PerfCounterOperationFailureStatus = "failure";

        static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(1);

        readonly AtomicBoolean closed;
        readonly CancellationTokenSource cts;
        readonly INotifierFactory notifierFactory;
        readonly AtomicReference<ImmutableDictionary<string, INotifier>> notifiers;
        readonly ISinkFactory<IMessage> sinkFactory;
        readonly AtomicReference<ImmutableDictionary<string, ISink<IMessage>>> sinks;
        readonly AsyncLock sync = new AsyncLock();
        readonly IRoutingPerfCounter routingPerformanceCounter;

        ImmutableDictionary<string, INotifier> Notifiers => this.notifiers;

        ImmutableDictionary<string, ISink<IMessage>> Sinks => this.sinks;

        public FrontendRoutingService(ISinkFactory<IMessage> sinkFactory, INotifierFactory notifierFactory)
            : this(sinkFactory, notifierFactory, null)
        { }

        public FrontendRoutingService(ISinkFactory<IMessage> sinkFactory, INotifierFactory notifierFactory, IRoutingPerfCounter routingPerformanceCounter)
        {
            this.sinkFactory = Preconditions.CheckNotNull(sinkFactory, nameof(sinkFactory));
            this.notifierFactory = Preconditions.CheckNotNull(notifierFactory, nameof(notifierFactory));
            this.routingPerformanceCounter = routingPerformanceCounter;
            this.closed = new AtomicBoolean(false);
            this.cts = new CancellationTokenSource();
            this.sinks = new AtomicReference<ImmutableDictionary<string, ISink<IMessage>>>(ImmutableDictionary<string, ISink<IMessage>>.Empty);
            this.notifiers = new AtomicReference<ImmutableDictionary<string, INotifier>>(ImmutableDictionary<string, INotifier>.Empty);
        }

        public Task RouteAsync(string hubName, IMessage message) => this.RouteAsync(hubName, new[] { message });

        public async Task RouteAsync(string hubName, IEnumerable<IMessage> messages)
        {
            this.CheckClosed();

            ISinkResult<IMessage> result;
            try
            {
                ISink<IMessage> sink = await this.GetSinkAsync(hubName);
                result = await sink.ProcessAsync(messages.ToArray(), this.cts.Token);
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                this.PerformanceCounterLogFailure(hubName, messages.Count());
                throw;
            }

            this.PerformanceCounterLogSuccess(hubName, result.Succeeded?.Count() ?? 0);
            this.PerformanceCounterLogFailure(hubName, result.Failed?.Count() ?? 0);

            // NOTE: we log succeeded however from the caller perspective all are failed (if any) due to next line
            result.SendFailureDetails.ForEach(sfd => { throw sfd.RawException; });
        }

        public Task<IEnumerable<EndpointHealthData>> GetEndpointHealthAsync(string hubName)
        {
            throw new NotSupportedException();
        }

        public Task StartAsync() => TaskEx.Done;

        public async Task CloseAsync(CancellationToken token)
        {
            if (!this.closed.GetAndSet(true))
            {
                using (await this.sync.LockAsync(this.cts.Token))
                {
                    this.cts.Cancel();
                    await Task.WhenAll(this.Notifiers.Values.Select(n => CloseNotifierAsync(n, token)));
                    await Task.WhenAll(this.Sinks.Values.Select(e => CloseSinkAsync(e, token)));
                }
            }
        }

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            Debug.Assert(this.closed);
            if (disposing)
            {
                this.cts.Dispose();
                this.sync.Dispose();

                foreach (INotifier notifier in this.Notifiers.Values)
                {
                    notifier.Dispose();
                }
            }
        }

        async Task<ISink<IMessage>> GetSinkAsync(string hubName)
        {
            ISink<IMessage> sink;
            ImmutableDictionary<string, ISink<IMessage>> snapshot = this.Sinks;

            if (!snapshot.TryGetValue(hubName, out sink))
            {
                using (await this.sync.LockAsync(this.cts.Token))
                {
                    snapshot = this.Sinks;
                    if (!snapshot.TryGetValue(hubName, out sink))
                    {
                        sink = await this.sinkFactory.CreateAsync(hubName);
                        bool sinkIsSet = this.sinks.CompareAndSet(snapshot, snapshot.Add(hubName, sink));
                        Debug.Assert(sinkIsSet);

                        // Setup to be notified of changes to the hub for this sink
                        INotifier notifier;
                        if (!this.Notifiers.TryGetValue(hubName, out notifier))
                        {
                            notifier = this.notifierFactory.Create(hubName);
                            await notifier.SubscribeAsync(nameof(FrontendRoutingService), this.UpdateSinkAsync, this.RemoveSinkAsync, this.cts.Token);
                            bool notifierIsSet = this.notifiers.CompareAndSet(this.Notifiers, this.Notifiers.Add(hubName, notifier));
                            Debug.Assert(notifierIsSet);
                        }
                    }
                }
            }
            return sink;
        }

        Task UpdateSinkAsync(string hubName) => TaskEx.Done;

        async Task RemoveSinkAsync(string hubName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ISink<IMessage> sink;
                INotifier notifier;

                using (await this.sync.LockAsync(this.cts.Token))
                {
                    // Remove the current evaluator
                    ImmutableDictionary<string, ISink<IMessage>> snapshot = this.Sinks;
                    if (snapshot.TryGetValue(hubName, out sink))
                    {
                        bool sinkIsSet = this.sinks.CompareAndSet(snapshot, snapshot.Remove(hubName));
                        Debug.Assert(sinkIsSet);
                    }

                    // Remove the current notifier
                    ImmutableDictionary<string, INotifier> notifierSnapshot = this.Notifiers;
                    if (notifierSnapshot.TryGetValue(hubName, out notifier))
                    {
                        // Remove it
                        bool notifierIsSet = this.notifiers.CompareAndSet(notifierSnapshot, notifierSnapshot.Remove(hubName));
                        Debug.Assert(notifierIsSet);
                    }
                }

                // Close the sink and notifier
                using (var timeoutCts = new CancellationTokenSource(OperationTimeout))
                using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, timeoutCts.Token))
                {
                    if (sink != null)
                    {
                        await CloseSinkAsync(sink, linkedCts.Token);
                    }

                    if (notifier != null)
                    {
                        await CloseNotifierAsync(notifier, linkedCts.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                Events.SinkRemoveFailed(ex, stopwatch);
            }
        }

        void CheckClosed()
        {
            if (this.closed)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "{0} is closed.", nameof(FrontendRoutingService)));
            }
        }

        static async Task CloseSinkAsync(ISink<IMessage> sink, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await sink.CloseAsync(token);
            }
            catch (Exception ex)
            {
                Events.SinkCloseFailed(ex, stopwatch);
            }
        }

        static async Task CloseNotifierAsync(INotifier notifier, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await notifier.CloseAsync(token);
            }
            catch (Exception ex)
            {
                Events.NotifierCloseFailed(notifier, ex, stopwatch);
            }
        }

        void PerformanceCounterLogFailure(string iotHubName, long count)
        {
            if (count == 0)
            {
                return;
            }

            string error;
            if (this.routingPerformanceCounter?.LogOperationResult(
                    iotHubName,
                    PerfCounterOperationName,
                    PerfCounterOperationFailureStatus,
                    count,
                    out error) == false)
            {
                // TODO: log perf counter failure
            }
        }

        void PerformanceCounterLogSuccess(string iotHubName, long count)
        {
            if (count == 0)
            {
                return;
            }

            string error;
            if (this.routingPerformanceCounter?.LogOperationResult(
                    iotHubName,
                    PerfCounterOperationName,
                    PerfCounterOperationSuccessStatus,
                    count,
                    out error) == false)
            {
                // TODO: log perf counter failure
            }
        }

        static class Events
        {
            const string Source = nameof(FrontendRoutingService);
            //static readonly ILog Log = Routing.Log;

            public static void SinkCloseFailed(Exception exception, Stopwatch stopwatch)
            {
                string latencyMs = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                //Log.Warning(nameof(SinkCloseFailed), Source, m => m("Sink close failed."), exception, string.Empty, string.Empty, latencyMs);
            }

            public static void SinkRemoveFailed(Exception exception, Stopwatch stopwatch)
            {
                string latencyMs = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                //Log.Warning(nameof(SinkRemoveFailed), Source, m => m("Sink removal failed while processing hub deletion."), exception, string.Empty, string.Empty, latencyMs);
            }

            public static void NotifierCloseFailed(INotifier notifier, Exception exception, Stopwatch stopwatch)
            {
                string latencyMs = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                //Log.Warning(nameof(NotifierCloseFailed), Source, m => m("Notifier close failed."), exception, notifier.IotHubName, string.Empty, latencyMs);
            }
        }
    }
}