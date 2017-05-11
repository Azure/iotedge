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
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;

    public class FilteringRoutingService : IRoutingService
    {
        static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(1);

        readonly AtomicBoolean closed;
        readonly IRouteCompiler compiler;
        readonly CancellationTokenSource cts;
        readonly AtomicReference<ImmutableDictionary<string, Evaluator>> evaluators;
        readonly AtomicReference<ImmutableDictionary<string, INotifier>> notifiers;
        readonly INotifierFactory notifierFactory;
        readonly IRouteStore routeStore;
        readonly AsyncLock sync;
        readonly IRoutingService underlying;

        ImmutableDictionary<string, Evaluator> Evaluators => this.evaluators;

        ImmutableDictionary<string, INotifier> Notifiers => this.notifiers;

        public FilteringRoutingService(IRoutingService underlying, IRouteStore routeStore, INotifierFactory notifierFactory)
            : this(underlying, routeStore, notifierFactory, RouteCompiler.Instance)
        {
        }

        public FilteringRoutingService(IRoutingService underlying, IRouteStore routeStore, INotifierFactory notifierFactory, IRouteCompiler compiler)
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            this.routeStore = Preconditions.CheckNotNull(routeStore, nameof(routeStore));
            this.notifierFactory = Preconditions.CheckNotNull(notifierFactory, nameof(notifierFactory));
            this.compiler = Preconditions.CheckNotNull(compiler, nameof(compiler));

            this.evaluators = new AtomicReference<ImmutableDictionary<string, Evaluator>>(ImmutableDictionary<string, Evaluator>.Empty);
            this.notifiers = new AtomicReference<ImmutableDictionary<string, INotifier>>(ImmutableDictionary<string, INotifier>.Empty);
            this.cts = new CancellationTokenSource();
            this.closed = new AtomicBoolean(false);
            this.sync = new AsyncLock();
        }

        public Task RouteAsync(string hubName, IMessage message) => this.RouteAsync(hubName, new[] { message });

        public async Task RouteAsync(string hubName, IEnumerable<IMessage> messages)
        {
            this.CheckClosed();

            Evaluator evaluator = await this.GetEvaluatorAsync(hubName);
            IList<IMessage> filtered = messages.Where(msg => evaluator.Evaluate(msg).Any()).ToList();

            if (filtered.Any())
            {
                await this.underlying.RouteAsync(hubName, filtered);
            }
        }

        public Task<IEnumerable<EndpointHealthData>> GetEndpointHealthAsync(string hubName) => this.underlying.GetEndpointHealthAsync(hubName);

        public Task StartAsync() => this.underlying.StartAsync();

        public async Task CloseAsync(CancellationToken token)
        {
            if (!this.closed.GetAndSet(true))
            {
                using (await this.sync.LockAsync(this.cts.Token))
                {
                    this.cts.Cancel();
                    ImmutableDictionary<string, Evaluator> snapshot = this.Evaluators;
                    await Task.WhenAll(this.Notifiers.Values.Select(n => CloseNotifierAsync(n, token)));
                    await Task.WhenAll(snapshot.Values.Select(e => CloseEvaluatorAsync(e, token)));
                    await this.underlying.CloseAsync(token);
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
                this.underlying.Dispose();
                foreach (INotifier notifier in this.Notifiers.Values)
                {
                    notifier.Dispose();
                }
            }
        }

        void CheckClosed()
        {
            if (this.closed)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "{0} is closed.", nameof(FilteringRoutingService)));
            }
        }

        static async Task CloseEvaluatorAsync(Evaluator evaluator, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await evaluator.CloseAsync(token);
            }
            catch (Exception ex)
            {
                Events.EvaluatorCloseFailed(ex, stopwatch);
            }
        }

        async Task<Evaluator> GetEvaluatorAsync(string hubName)
        {
            ImmutableDictionary<string, Evaluator> snapshot = this.Evaluators;

            Evaluator evaluator;
            if (!snapshot.TryGetValue(hubName, out evaluator))
            {
                using (await this.sync.LockAsync(this.cts.Token))
                {
                    snapshot = this.Evaluators;
                    if (!snapshot.TryGetValue(hubName, out evaluator))
                    {
                        RouterConfig config = await this.routeStore.GetRouterConfigAsync(hubName, this.cts.Token);
                        evaluator = new Evaluator(config, this.compiler);
                        bool evaluatorIsSet = this.evaluators.CompareAndSet(snapshot, snapshot.Add(hubName, evaluator));
                        Debug.Assert(evaluatorIsSet);

                        // Setup to be notified of changes to the hub for this evaluator
                        INotifier notifier;
                        if (!this.Notifiers.TryGetValue(hubName, out notifier))
                        {
                            notifier = this.notifierFactory.Create(hubName);
                            await notifier.SubscribeAsync(nameof(FilteringRoutingService), this.UpdateEvaluatorAsync, this.RemoveEvaluatorAsync, this.cts.Token);
                            bool notifierIsSet = this.notifiers.CompareAndSet(this.Notifiers, this.Notifiers.Add(hubName, notifier));
                            Debug.Assert(notifierIsSet);
                        }
                    }
                }
            }
            return evaluator;
        }

        async Task UpdateEvaluatorAsync(string hubName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                bool shouldClose;
                Evaluator oldEvaluator;
                using (await this.sync.LockAsync(this.cts.Token))
                {
                    ImmutableDictionary<string, Evaluator> snapshot = this.Evaluators;
                    RouterConfig config = await this.routeStore.GetRouterConfigAsync(hubName, this.cts.Token);

                    // Close the current evaluator
                    shouldClose = snapshot.TryGetValue(hubName, out oldEvaluator);

                    var newEvaluator = new Evaluator(config);
                    bool evaluatorIsSet = this.evaluators.CompareAndSet(snapshot, snapshot.SetItem(hubName, newEvaluator));
                    Debug.Assert(evaluatorIsSet);
                }

                if (shouldClose)
                {
                    await oldEvaluator.CloseAsync(this.cts.Token);
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.EvaluatorUpdateFailed(hubName, ex, stopwatch);
            }
        }

        async Task RemoveEvaluatorAsync(string hubName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                Evaluator evaluator;
                INotifier notifier = null;

                using (await this.sync.LockAsync(this.cts.Token))
                {
                    ImmutableDictionary<string, Evaluator> evaluatorSnapshot = this.Evaluators;
                    ImmutableDictionary<string, INotifier> notifierSnapshot = this.Notifiers;

                    // Close and remove the current evaluator
                    if (evaluatorSnapshot.TryGetValue(hubName, out evaluator))
                    {
                        bool evaluatorIsSet = this.evaluators.CompareAndSet(evaluatorSnapshot, evaluatorSnapshot.Remove(hubName));
                        Debug.Assert(evaluatorIsSet);

                        // Close and remove the current notifier
                        if (notifierSnapshot.TryGetValue(hubName, out notifier))
                        {
                            bool notifierIsSet = this.notifiers.CompareAndSet(notifierSnapshot, notifierSnapshot.Remove(hubName));
                            Debug.Assert(notifierIsSet);
                        }
                    }
                }

                using (var timeoutCts = new CancellationTokenSource(OperationTimeout))
                using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, timeoutCts.Token))
                {
                    if (evaluator != null)
                    {
                        await CloseEvaluatorAsync(evaluator, linkedCts.Token);
                    }

                    if (notifier != null)
                    {
                        await CloseNotifierAsync(notifier, linkedCts.Token);
                    }
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.EvaluatorRemoveFailed(hubName, ex, stopwatch);
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

        static class Events
        {
            const string Source = nameof(FilteringRoutingService);
            //static readonly ILog Log = Routing.Log;

            public static void EvaluatorCloseFailed(Exception exception, Stopwatch stopwatch)
            {
                //string latencyMs = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                //Log.Warning(nameof(EvaluatorCloseFailed), Source, m => m("Evaluator close failed."), exception, string.Empty, string.Empty, latencyMs);
            }

            public static void EvaluatorUpdateFailed(string hubName, Exception exception, Stopwatch stopwatch)
            {
                //string latencyMs = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                //Log.Warning(nameof(EvaluatorUpdateFailed), Source, m => m("Evaluator update failed."), exception, hubName, string.Empty, latencyMs);
            }

            public static void EvaluatorRemoveFailed(string hubName, Exception exception, Stopwatch stopwatch)
            {
                //string latencyMs = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                //Log.Warning(nameof(EvaluatorRemoveFailed), Source, m => m("Evaluator remove failed."), exception, hubName, string.Empty, latencyMs);
            }

            public static void NotifierCloseFailed(INotifier notifier, Exception exception, Stopwatch stopwatch)
            {
                //string latencyMs = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                //Log.Warning(nameof(NotifierCloseFailed), Source, m => m("Notifier close failed."), exception, notifier.IotHubName, string.Empty, latencyMs);
            }
        }
    }
}