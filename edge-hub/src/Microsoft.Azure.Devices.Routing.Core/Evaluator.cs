// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using static System.FormattableString;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class Evaluator
    {
        static readonly ISet<Endpoint> NoEndpoints = ImmutableHashSet<Endpoint>.Empty;

        readonly object sync = new object();
        // ReSharper disable once InconsistentlySynchronizedField - compiledRoutes is immutable
        readonly AtomicReference<ImmutableDictionary<string, CompiledRoute>> compiledRoutes;
        readonly IRouteCompiler compiler;
        readonly Option<CompiledRoute> fallback;

        // Because we are only reading here, it doesn't matter that it is under a lock
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
            var endpoints = new HashSet<Endpoint>();

            // Because we are only reading here, it doesn't matter that it is under a lock
            // ReSharper disable once InconsistentlySynchronizedField - compiledRoutes is immutable
            ImmutableDictionary<string, CompiledRoute> snapshot = this.compiledRoutes;
            foreach (CompiledRoute compiledRoute in snapshot.Values.Where(cr => cr.Route.Source.Match(message.MessageSource)))
            {
                if (EvaluateInternal(compiledRoute, message))
                {
                    endpoints.UnionWith(compiledRoute.Route.Endpoints);
                }
            }

            // only use the fallback for telemetry messages
            if (endpoints.Any() || !message.MessageSource.IsTelemetry())
            {
                return endpoints;
            }
            else
            {
                // Handle fallback case
                ISet<Endpoint> fallbackEndpoints = this.fallback
                    .Filter(cr => EvaluateInternal(cr, message))
                    .Map(cr => cr.Route.Endpoints)
                    .GetOrElse(NoEndpoints);

                if (fallbackEndpoints.Any())
                {
                    Events.EvaluateFallback(fallbackEndpoints.First());
                }

                return fallbackEndpoints;
            }
        }

        static bool EvaluateInternal(CompiledRoute compiledRoute, IMessage message)
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
                Events.EvaluateFailure(compiledRoute.Route, ex);
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
            Events.Compile(route);

            try
            {
                // Setting all flags for the compiler assuming this will only be invoked at runtime.
                Func<IMessage, Bool> evaluate = this.compiler.Compile(route, RouteCompilerFlags.All);
                var result = new CompiledRoute(route, evaluate);
                Events.CompileSuccess(route);
                return result;
            }
            catch (Exception ex)
            {
                Events.CompileFailure(route, ex);
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
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<Evaluator>();
            const int IdStart = Routing.EventIds.Evaluator;

            enum EventIds
            {
                Compile = IdStart,
                CompileSuccess,
                CompileFailure,
                EvaluatorFailure,
            }

            public static void Compile(Route route)
            {
                Log.LogInformation((int)EventIds.Compile, "[Compile] {0}", GetMessage("Compile began.", route));
            }

            public static void CompileSuccess(Route route)
            {
                Log.LogInformation((int)EventIds.CompileSuccess, "[CompileSuccess] {0}", GetMessage("Compile succeeded.", route));
            }

            public static void CompileFailure(Route route, Exception ex)
            {
                Log.LogError((int)EventIds.CompileFailure, ex, "[CompileFailure] {0}", GetMessage("Compile failed.", route));
            }

            public static void EvaluateFailure(Route route, Exception ex)
            {
                Log.LogError((int)EventIds.EvaluatorFailure, ex, "[EvaluateFailure] {0}", GetMessage("Evaluate failed.", route));
            }

            public static void EvaluateFallback(Endpoint endpoint)
            {
                Routing.UserMetricLogger.LogEgressFallbackMetric(1, endpoint.IotHubName);
            }

            static string GetMessage(string message, Route route)
            {
                return Invariant($"{message} RouteId: \"{route.Id}\" RouteCondition: \"{route.Condition}\"");
            }
        }
    }
}
