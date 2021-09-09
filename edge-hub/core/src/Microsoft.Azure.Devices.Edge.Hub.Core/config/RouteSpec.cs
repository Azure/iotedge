// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Newtonsoft.Json;

    public class RouteSpec
    {
        // For legacy route definitions, we use 0 for the TTL.
        // Since messages can't realistically have zero TTL,
        // we use it as a special value to signify that the TTL
        // property was not specified, in which case the global
        // TTL value in StoreAndFowardConfiguration will kick in.
        public RouteSpec(string route)
            : this(route, RouteFactory.DefaultPriority, 0)
        {
        }

        [JsonConstructor]
        public RouteSpec(string route, uint priority, uint timeToLiveSecs)
        {
            this.Route = Preconditions.CheckNonWhiteSpace(route, nameof(route));
            this.TimeToLiveSecs = timeToLiveSecs;

            // Verify the route, this must be either between [0-9], or the default value
            if ((priority < 0 || priority > 9) && priority != RouteFactory.DefaultPriority)
            {
                throw new ArgumentOutOfRangeException(nameof(priority), priority, $"Invalid priority for route: {route}, priority can only be between 0-9");
            }

            this.Priority = priority;
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
