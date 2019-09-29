// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Newtonsoft.Json;

    // TODO: add tests
    public class CombinedDockerConfigToStringConverter : JsonConverter<CombinedDockerConfig>
    {
        public override void WriteJson(JsonWriter writer, CombinedDockerConfig value, JsonSerializer serializer)
        {
            using (var text = new StringWriter())
            {
                serializer.Serialize(text, value);
                writer.WriteValue(text.ToString());
            }
        }

        public override CombinedDockerConfig ReadJson(JsonReader reader, Type objectType, CombinedDockerConfig existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            using (var text = new StringReader(reader.Value.ToString()))
            {
                return (CombinedDockerConfig)serializer.Deserialize(text, typeof(CombinedDockerConfig));
            }
        }
    }
}
