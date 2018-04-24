// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using global::Docker.DotNet.Models;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using CoreConstants = Core.Constants;

    [JsonConverter(typeof(DockerReportedConfigJsonConverter))]
    public class DockerReportedConfig : DockerConfig, IEquatable<DockerReportedConfig>
    {
        public static DockerReportedConfig Unknown = new DockerReportedConfig(CoreConstants.Unknown, string.Empty, string.Empty);

        [JsonProperty(PropertyName = "imageHash")]
        public string ImageHash { get; }

        [JsonConstructor]
        public DockerReportedConfig(string image, string createOptions, string imageHash)
            : base(image, createOptions)
        {
            this.ImageHash = imageHash ?? string.Empty;
        }

        public DockerReportedConfig(string image, CreateContainerParameters createOptions, string imageHash)
            : base(image, createOptions)
        {
            this.ImageHash = imageHash ?? string.Empty;
        }

        public override bool Equals(object obj) => this.Equals(obj as DockerReportedConfig);

        public bool Equals(DockerReportedConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && string.Equals(this.ImageHash, other.ImageHash);
        }

        public override int GetHashCode()
        {
            int hashCode = 1110516558;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.ImageHash);
            return hashCode;
        }

        class DockerReportedConfigJsonConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();

                var dockerReportedConfig = (DockerReportedConfig)value;

                writer.WritePropertyName("image");
                serializer.Serialize(writer, dockerReportedConfig.Image);

                writer.WritePropertyName("imageHash");
                serializer.Serialize(writer, dockerReportedConfig.ImageHash);

                writer.WritePropertyName("createOptions");
                serializer.Serialize(writer, JsonConvert.SerializeObject(dockerReportedConfig.CreateOptions));

                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject obj = JObject.Load(reader);

                // Pull out JToken values from json
                obj.TryGetValue("image", StringComparison.OrdinalIgnoreCase, out JToken jTokenImage);
                obj.TryGetValue("imageHash", StringComparison.OrdinalIgnoreCase, out JToken jTokenImageHash);
                obj.TryGetValue("createOptions", StringComparison.OrdinalIgnoreCase, out JToken jTokenCreateOptions);

                return new DockerReportedConfig(jTokenImage?.ToString(), (jTokenCreateOptions?.ToString() ?? string.Empty), jTokenImageHash?.ToString());
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(DockerReportedConfig);
        }
    }
}
