// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class RouteConfigurationConverter : JsonConverter<IDictionary<string, RouteConfiguration>>
    {
        public override void WriteJson(JsonWriter writer, IDictionary<string, RouteConfiguration> value, JsonSerializer serializer) => throw new NotSupportedException();

        public override IDictionary<string, RouteConfiguration> ReadJson(JsonReader reader, Type type, IDictionary<string, RouteConfiguration> value, bool hasValue, JsonSerializer serializer)
        {
            var routes = new Dictionary<string, RouteConfiguration>();
            JToken token = JToken.Load(reader);

            foreach (JToken child in token.Children())
            {
                // The route name must not be empty
                if (string.IsNullOrWhiteSpace(child.First.Path))
                {
                    throw new InvalidDataException($"Empty route name in {child.ToString()}");
                }

                if (child.First.Type == JTokenType.String)
                {
                    // Legacy routes are just string properties
                    routes.Add(child.First.Path, new RouteConfiguration(child.First.ToObject<string>()));
                }
                else if (child.First.Type == JTokenType.Object)
                {
                    // Newer routes are objects with their own properties
                    routes.Add(child.First.Path, child.First.ToObject<RouteConfiguration>());
                }
                else
                {
                    throw new InvalidDataException($"Malformed route specification: {child.ToString()}");
                }
            }

            return routes;
        }
    }
}
