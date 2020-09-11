// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query;

    public abstract class RouteFactory
    {
        // Default route priority at half of uint.MaxValue,
        // so that routes with no custom priority fall below
        // everything else, and reserve room for routes that
        // are below default priority.  We round down to an
        // even 2bn for easy remembering.
        public const uint DefaultPriority = 2000000000;

        const string DefaultCondition = "true";

        readonly IEndpointFactory endpointFactory;

        protected RouteFactory(IEndpointFactory endpointFactory)
        {
            this.endpointFactory = Preconditions.CheckNotNull(endpointFactory, nameof(endpointFactory));
        }

        public abstract string IotHubName { get; }

        public abstract string GetNextRouteId();

        // If TTL is not provided for the route, we just set it
        // to zero so that the global TTL value takes over
        public Route Create(string routeString) =>
            this.Create(routeString, DefaultPriority, 0);

        public Route Create(string routeString, uint priority, uint timeToLiveSecs)
        {
            // Parse route into constituents
            this.ParseRoute(Preconditions.CheckNotNull(routeString, nameof(routeString)), out IMessageSource messageSource, out string condition, out Endpoint endpoint);
            var route = new Route(this.GetNextRouteId(), condition, this.IotHubName, messageSource, endpoint, priority, timeToLiveSecs);
            return route;
        }

        public IEnumerable<Route> Create(IEnumerable<string> routes)
        {
            return Preconditions.CheckNotNull(routes, nameof(routes))
                .Select(r => this.Create(r));
        }

        internal void ParseRoute(string routeString, out IMessageSource messageSource, out string condition, out Endpoint endpoint)
        {
            var errorListener = new ErrorListener();
            var input = new AntlrInputStream(routeString);
            var lexer = new RouteLexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new RouteParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            IParseTree tree = parser.route();
            errorListener.Validate();

            var walker = new ParseTreeWalker();
            var listener = new RouteParserListener(this.endpointFactory);
            walker.Walk(listener, tree);

            condition = listener.Condition ?? DefaultCondition;
            messageSource = CustomMessageSource.Create(listener.Source);
            endpoint = listener.Endpoint;
        }

        class RouteParserListener : RouteBaseListener
        {
            readonly IEndpointFactory endpointFactory;

            public RouteParserListener(IEndpointFactory endpointFactory)
            {
                this.endpointFactory = endpointFactory;
            }

            public string Source { get; private set; }

            public Endpoint Endpoint { get; private set; }

            public string Condition { get; private set; }

            public override void ExitSource(RouteParser.SourceContext context)
            {
                this.Source = context.GetText();
            }

            public override void ExitRoutecondition(RouteParser.RouteconditionContext context)
            {
                ICharStream stream = context.Start.InputStream;
                int startIndex = context.Start.StartIndex;
                int endIndex = context.Stop.StopIndex;

                var interval = new Interval(startIndex, endIndex);
                this.Condition = stream.GetText(interval);
            }

            public override void ExitSystemEndpoint(RouteParser.SystemEndpointContext context)
            {
                string systemEndpoint = context.GetText();
                Endpoint endpoint = this.endpointFactory.CreateSystemEndpoint(systemEndpoint);
                this.Endpoint = endpoint;
            }

            public override void ExitFuncEndpoint(RouteParser.FuncEndpointContext context)
            {
                string funcName = context.func.Text;
                string address = context.endpoint.Text.Substring(1, context.endpoint.Text.Length - 2);
                Endpoint endpoint = this.endpointFactory.CreateFunctionEndpoint(funcName, address);
                this.Endpoint = endpoint;
            }
        }
    }
}
