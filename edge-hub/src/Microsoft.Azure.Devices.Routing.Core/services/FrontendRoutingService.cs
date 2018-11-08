// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class FrontendRoutingService : IRoutingService
    {
        static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(1);

        readonly AtomicBoolean closed;
        readonly CancellationTokenSource cts;
        readonly INotifierFactory notifierFactory;
        readonly AtomicReference<ImmutableDictionary<string, INotifier>> notifiers;
        readonly ISinkFactory<IMessage> sinkFactory;
        readonly AtomicReference<ImmutableDictionary<string, ISink<IMessage>>> sinks;
        readonly AsyncLock sync = new AsyncLock();

        public FrontendRoutingService(ISinkFactory<IMessage> sinkFactory, INotifierFactory notifierFactory)
        {
            this.sinkFactory = Preconditions.CheckNotNull(sinkFactory, nameof(sinkFactory));
            this.notifierFactory = Preconditions.CheckNotNull(notifierFactory, nameof(notifierFactory));
            this.closed = new AtomicBoolean(false);
            this.cts = new CancellationTokenSource();
            this.sinks = new AtomicReference<ImmutableDictionary<string, ISink<IMessage>>>(ImmutableDictionary<string, ISink<IMessage>>.Empty);
            this.notifiers = new AtomicReference<ImmutableDictionary<string, INotifier>>(ImmutableDictionary<string, INotifier>.Empty);
        }

        ImmutableDictionary<string, INotifier> Notifiers => this.notifiers;

        ImmutableDictionary<string, ISink<IMessage>> Sinks => this.sinks;

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

        public Task<IEnumerable<EndpointHealthData>> GetEndpointHealthAsync(string hubName)
        {
            throw new NotSupportedException();
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
                throw;
            }

            // NOTE: we log succeeded however from the caller perspective all are failed (if any) due to next line
            result.SendFailureDetails.ForEach(sfd => throw sfd.RawException);
        }

        public Task StartAsync() => TaskEx.Done;

        protected virtual void Dispose(bool disposing)
        {
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

        static async Task CloseNotifierAsync(INotifier notifier, CancellationToken token)
        {
            try
            {
                await notifier.CloseAsync(token);
            }
            catch (Exception ex)
            {
                Events.NotifierCloseFailed(ex);
            }
        }

        static async Task CloseSinkAsync(ISink<IMessage> sink, CancellationToken token)
        {
            try
            {
                await sink.CloseAsync(token);
            }
            catch (Exception ex)
            {
                Events.SinkCloseFailed(ex);
            }
        }

        void CheckClosed()
        {
            if (this.closed)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "{0} is closed.", nameof(FrontendRoutingService)));
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
                        Debug.Assert(sinkIsSet, "sinkIsSet");

                        // Setup to be notified of changes to the hub for this sink
                        INotifier notifier;
                        if (!this.Notifiers.TryGetValue(hubName, out notifier))
                        {
                            notifier = this.notifierFactory.Create(hubName);
                            await notifier.SubscribeAsync(nameof(FrontendRoutingService), this.UpdateSinkAsync, this.RemoveSinkAsync, this.cts.Token);
                            bool notifierIsSet = this.notifiers.CompareAndSet(this.Notifiers, this.Notifiers.Add(hubName, notifier));
                            Debug.Assert(notifierIsSet, "notifierIsSet");
                        }
                    }
                }
            }

            return sink;
        }

        async Task RemoveSinkAsync(string hubName)
        {
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
                        Debug.Assert(sinkIsSet, "sinkIsSet");
                    }

                    // Remove the current notifier
                    ImmutableDictionary<string, INotifier> notifierSnapshot = this.Notifiers;
                    if (notifierSnapshot.TryGetValue(hubName, out notifier))
                    {
                        // Remove it
                        bool notifierIsSet = this.notifiers.CompareAndSet(notifierSnapshot, notifierSnapshot.Remove(hubName));
                        Debug.Assert(notifierIsSet, "notifierIsSet");
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
                Events.SinkRemoveFailed(ex);
            }
        }

        Task UpdateSinkAsync(string hubName) => TaskEx.Done;

        static class Events
        {
            const int IdStart = Routing.EventIds.FrontendRoutingService;
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<FrontendRoutingService>();

            enum EventIds
            {
                SinkCloseFailed = IdStart,
                SinkRemoveFailed,
                NotifierCloseFailed,
            }

            public static void NotifierCloseFailed(Exception exception)
            {
                Log.LogWarning((int)EventIds.NotifierCloseFailed, exception, "[NotifierCloseFailed] Notifier close failed.");
            }

            public static void SinkCloseFailed(Exception exception)
            {
                Log.LogWarning((int)EventIds.SinkCloseFailed, exception, "[SinkCloseFailed] Sink close failed.");
            }

            public static void SinkRemoveFailed(Exception exception)
            {
                Log.LogWarning((int)EventIds.SinkRemoveFailed, exception, "[SinkRemoveFailed] Sink removal failed while processing hub deletion.");
            }
        }
    }
}
