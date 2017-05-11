// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
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

    public class Evaluator
    {
        static readonly ISet<Endpoint> NoEndpoints = ImmutableHashSet<Endpoint>.Empty;

        readonly object sync = new object();
        readonly AtomicReference<ImmutableDictionary<string, CompiledRoute>> compiledRoutes;
        readonly IRouteCompiler compiler;
        readonly Option<CompiledRoute> fallback;

        // ReSharper disable once InconsistentlySynchronizedField - compiledRoutes is immutable
        public ISet<Route> Routes
        {
            get
            {
                ImmutableDictionary<string, CompiledRoute> snapshot = this.compiledRoutes;
                return new HashSet<Route>(snapshot.Values.Select(c => c.Route));
            }
        }

        public Evaluator(RouterConfig config)
            : this(config, RouteCompiler.Instance)
        {
        }

        public Evaluator(RouterConfig config, IRouteCompiler compiler)
        {
            Preconditions.CheckNotNull(config, nameof(config));
            this.compiler = Preconditions.CheckNotNull(compiler);
            this.fallback = config.Fallback.Map(this.Compile);

            ImmutableDictionary<string, CompiledRoute> routesDict = config
                .Routes
                .ToImmutableDictionary(r => r.Id, r => this.Compile(r));
            this.compiledRoutes = new AtomicReference<ImmutableDictionary<string, CompiledRoute>>(routesDict);
        }

        public ISet<Endpoint> Evaluate(IMessage message)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var endpoints = new HashSet<Endpoint>();

            ImmutableDictionary<string, CompiledRoute> snapshot = this.compiledRoutes;
            foreach (CompiledRoute compiledRoute in snapshot.Values.Where(cr => cr.Route.Source == message.MessageSource))
            {
                if (EvaluateInternal(compiledRoute, message, stopwatch))
                {
                    endpoints.UnionWith(compiledRoute.Route.Endpoints);
                }
            }

            // only use the fallback for telemetry messages
            if (endpoints.Any() || message.MessageSource != MessageSource.Telemetry)
            {
                return endpoints;
            }
            else
            {
                // Handle fallback case
                ISet<Endpoint> fallbackEndpoints = this.fallback
                    .Filter(cr => EvaluateInternal(cr, message, stopwatch))
                    .Map(cr => cr.Route.Endpoints)
                    .GetOrElse(NoEndpoints);

                if (fallbackEndpoints.Any())
                {
                    Events.EvaluateFallback(fallbackEndpoints.First());
                }

                return fallbackEndpoints;
            }
        }

        static bool EvaluateInternal(CompiledRoute compiledRoute, IMessage message, Stopwatch stopwatch)
        {
            try
            {
                Bool evaluation = compiledRoute.Evaluate(message);

                if (evaluation.Equals(Bool.Undefined))
                {
                    Routing.UserAnalyticsLogger.LogUndefinedRouteEvaluation(message, compiledRoute.Route);
                }

                return evaluation;
            }
            catch (Exception ex)
            {
                Events.EvaluateFailure(compiledRoute.Route, ex, stopwatch);
                throw;
            }
        }

        public void SetRoute(Route route)
        {
            lock (this.sync)
            {
                ImmutableDictionary<string, CompiledRoute> snapshot = this.compiledRoutes;
                this.compiledRoutes.GetAndSet(snapshot.SetItem(route.Id, this.Compile(route)));
            }
        }

        public void RemoveRoute(string id)
        {
            lock (this.sync)
            {
                ImmutableDictionary<string, CompiledRoute> snapshot = this.compiledRoutes;
                this.compiledRoutes.GetAndSet(snapshot.Remove(id));
            }
        }

        public void ReplaceRoutes(ISet<Route> newRoutes)
        {
            lock (this.sync)
            {
                ImmutableDictionary<string, CompiledRoute> routesDict = Preconditions.CheckNotNull(newRoutes)
                    .ToImmutableDictionary(r => r.Id, r => this.Compile(r));
                this.compiledRoutes.GetAndSet(routesDict);
            }
        }

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;

        CompiledRoute Compile(Route route)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Events.Compile(route);

            try
            {
                // Setting all flags for the compiler assuming this will only be invoked at runtime.
                Func<IMessage, Bool> evaluate = this.compiler.Compile(route, RouteCompilerFlags.All);
                var result = new CompiledRoute(route, evaluate);
                Events.CompileSuccess(route, stopwatch);
                return result;
            }
            catch (Exception ex)
            {
                Events.CompileFailure(route, ex, stopwatch);
                throw;
            }
        }

        class CompiledRoute
        {
            public Route Route { get; }

            public Func<IMessage, Bool> Evaluate { get; }

            public CompiledRoute(Route route, Func<IMessage, Bool> evaluate)
            {
                this.Route = Preconditions.CheckNotNull(route);
                this.Evaluate = Preconditions.CheckNotNull(evaluate);
            }
        }

        static class Events
        {
            const string Source = nameof(Evaluator);
            const string DeviceId = "NotAvailable";

            //static readonly ILog Log = Routing.Log;

            public static void Compile(Route route)
            {
                //Log.Informational(nameof(Compile), Source, GetMessage("Compile began.", route), route.IotHubName, DeviceId);
            }

            public static void CompileSuccess(Route route, Stopwatch stopwatch)
            {
                //Log.Informational(nameof(CompileSuccess), Source, GetMessage("Compile succeeded.", route),
                //    route.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void CompileFailure(Route route, Exception ex, Stopwatch stopwatch)
            {
                //Log.Error(nameof(CompileFailure), Source, GetMessage("Compile failed.", route),
                //    ex, route.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void EvaluateFailure(Route route, Exception ex, Stopwatch stopwatch)
            {
                //Log.Error(nameof(EvaluateFailure), Source, GetMessage("Evaluate failed.", route),
                //    ex, route.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void EvaluateFallback(Endpoint endpoint)
            {
                Routing.UserMetricLogger.LogEgressFallbackMetric(1, endpoint.IotHubName);
            }

            static string GetMessage(string message, Route route)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} RouteId: \"{1}\" RouteCondition: \"{2}\"", message, route.Id, route.Condition);
            }
        }
    }
}
