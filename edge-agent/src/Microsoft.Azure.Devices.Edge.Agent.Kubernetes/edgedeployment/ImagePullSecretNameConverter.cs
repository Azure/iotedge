// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using Newtonsoft.Json;

    public class ImagePullSecretNameConverter : JsonConverter<ImagePullSecretName>
    {
        public override void WriteJson(JsonWriter writer, ImagePullSecretName value, JsonSerializer serializer) => writer.WriteValue(value.ToString());

        public override ImagePullSecretName ReadJson(JsonReader reader, Type objectType, ImagePullSecretName existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String)
            {
                throw new JsonSerializationException($"Unable to deserialize {typeof(ImagePullSecretName)}");
            }

            return new ImagePullSecretName(reader.Value.ToString());
        }
    }
}
