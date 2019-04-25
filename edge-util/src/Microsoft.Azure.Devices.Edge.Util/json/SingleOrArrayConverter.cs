// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Json
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class SingleOrArrayConverter<T> : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            value = value is List<T> list && list.Count == 1
                ? list[0]
                : value;
            serializer.Serialize(writer, value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            return token.Type == JTokenType.Array
                ? token.ToObject<List<T>>()
                : new List<T> { token.ToObject<T>() };
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(List<T>);
    }
}
