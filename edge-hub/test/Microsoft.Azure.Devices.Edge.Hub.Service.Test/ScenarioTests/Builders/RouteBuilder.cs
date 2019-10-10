// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;

    public class RouteBuilder
    {
        private readonly List<Routing.Core.Endpoint> explicitEndpoints = new List<Routing.Core.Endpoint>();
        private readonly List<EndpointBuilder> toBeBuiltEndpoints = new List<EndpointBuilder>();

        // the default behavior is that if nothing specified, this class adds an endpoint
        // however if the user explicitly says they don't need any endpoint, this flag shows that
        private bool withEndpoints = true;

        private string id = "test-route-" + NextRouteNumber();
        private string condition = "true";
        private string iotHubName = "test-hub";

        // many times the only customization is the proxy, so create a shortcut at this level, it creates a
        // default pipeline with the custom proxy at the end of the chain. Might or might not be used.
        private Func<string, Task<Option<ICloudProxy>>> cloudProxyGetterFunc = null;

        private IMessageSource messageSource = TelemetryMessageSource.Instance;

        static int routeCounter;
        static int NextRouteNumber() => Interlocked.Increment(ref routeCounter);

        public static RouteBuilder Create() => new RouteBuilder();

        public RouteBuilder WithNoEndpoints()
        {
            this.withEndpoints = false;
            return this;
        }

        public RouteBuilder WithEndpoint(Routing.Core.Endpoint endpoint)
        {
            this.explicitEndpoints.Add(endpoint);
            return this;
        }

        public RouteBuilder WithEndpoint(Func<EndpointBuilder, EndpointBuilder> builder)
        {
            this.toBeBuiltEndpoints.Add(builder(EndpointBuilder.Create()));
            return this;
        }

        public RouteBuilder WithId(string id)
        {
            this.id = id;
            return this;
        }

        public RouteBuilder WithCondition(string condition)
        {
            this.condition = condition;
            return this;
        }

        public RouteBuilder WithHubName(string iotHubName)
        {
            this.iotHubName = iotHubName;
            return this;
        }

        public RouteBuilder WithMessageSource(IMessageSource messageSource)
        {
            this.messageSource = messageSource;
            return this;
        }

        public RouteBuilder WithProxy<T>()
            where T : ICloudProxy, new()
        {
            this.cloudProxyGetterFunc = _ => Task.FromResult(Option.Some(new T() as ICloudProxy));
            return this;
        }

        public RouteBuilder WithProxyGetter(Func<string, Task<Option<ICloudProxy>>> getter)
        {
            this.cloudProxyGetterFunc = getter;
            return this;
        }

        public Routing.Core.Route Build()
        {
            var endpoints = new HashSet<Routing.Core.Endpoint>();
            if (this.withEndpoints)
            {
                if (!this.explicitEndpoints.Any() && !this.toBeBuiltEndpoints.Any() && this.cloudProxyGetterFunc == null)
                {
                    endpoints.Add(EndpointBuilder.Create().Build());
                }
                else
                {
                    this.explicitEndpoints.ForEach(e => endpoints.Add(e));
                    this.toBeBuiltEndpoints.ForEach(e => endpoints.Add(e.Build()));

                    if (this.cloudProxyGetterFunc != null)
                    {
                        endpoints.Add(EndpointBuilder.Create().WithProxyGetter(this.cloudProxyGetterFunc).Build());
                    }
                }
            }

            var result = new Routing.Core.Route(
                                this.id,
                                this.condition,
                                this.iotHubName,
                                this.messageSource,
                                endpoints);
            return result;
        }
    }
}
