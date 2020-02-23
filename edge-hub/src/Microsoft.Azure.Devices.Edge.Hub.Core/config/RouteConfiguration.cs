// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Newtonsoft.Json;

    public class RouteConfiguration
    {
        // For legacy route definitions, we use 0 for the TTL.
        // Since messages can't realistically have zero TTL,
        // we use it as a special value to signify that the TTL
        // property was not specified, in which case the global
        // TTL value in StoreAndFowardConfiguration will kick in.
        public RouteConfiguration(string route)
            : this(route, RouteFactory.DefaultPriority, 0)
        {
        }

        [JsonConstructor]
        public RouteConfiguration(string route, uint priority, uint timeToLiveSecs)
        {
            this.Route = Preconditions.CheckNonWhiteSpace(route, nameof(route));
            this.Priority = priority;
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
