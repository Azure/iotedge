// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    public class ObjectToStringConverter<T> : JsonConverter<T>
    {
        public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
        {
            using (var text = new StringWriter())
            {
                serializer.Serialize(text, value);
                writer.WriteValue(text.ToString());
            }
        }

        public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            using (var text = new StringReader(reader.Value.ToString()))
            {
                return (T)serializer.Deserialize(text, typeof(T));
            }
        }
    }
}
