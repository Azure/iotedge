// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Routing.Core;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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

    public class RouteConfigurationDictionaryConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();

        public override object ReadJson(JsonReader reader, Type type, object value, JsonSerializer serializer)
        {
            var routes = new Dictionary<string, RouteConfiguration>();
            JToken token = JToken.Load(reader);

            foreach (JToken child in token.Children())
            {
                if (child.First.Type == JTokenType.String)
                {
                    // Legacy routes are just string properties
                    routes.Add(child.First.Path, new RouteConfiguration(child.First.ToObject<string>()));
                }
                else
                {
                    // Newer routes are objects with their own properties
                    routes.Add(child.First.Path, child.First.ToObject<RouteConfiguration>());
                }
            }

            return routes;
        }

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => false;
    }
}
