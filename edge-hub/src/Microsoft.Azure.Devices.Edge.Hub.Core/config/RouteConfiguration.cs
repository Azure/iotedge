// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Routing.Core;
    using Newtonsoft.Json;

    public class RouteConfiguration
    {
        public RouteConfiguration(string route)
            : this(route, RouteFactory.DefaultPriority, 0)
        {
        }

        [JsonConstructor]
        public RouteConfiguration(string route, uint priority, uint timeToLiveSecs)
        {
            this.Route = route;
            this.Priority = priority == 0 ? RouteFactory.DefaultPriority : priority;
            this.TimeToLiveSecs = timeToLiveSecs;
        }

        [JsonProperty(PropertyName = "route")]
        public string Route { get; }

        [DefaultValue(RouteFactory.DefaultPriority)]
        [JsonProperty(PropertyName = "priority", DefaultValueHandling = DefaultValueHandling.Populate)]
        public uint Priority { get; }

        [JsonProperty(PropertyName = "timeToLiveSecs")]
        public uint TimeToLiveSecs { get; }
    }
}
