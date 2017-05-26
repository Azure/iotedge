// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class JsonEx
    {
        public static T Get<T>(this JObject obj, string key)
        {
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken jTokenValue))
            {
                throw new JsonSerializationException($"Could not find {key} in JObject.");
            }
            return jTokenValue.Value<T>();
        }
    }
}