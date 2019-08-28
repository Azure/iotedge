// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;

    [JsonConverter(typeof(DockerReportedConfigJsonConverter))]
    public class DockerReportedConfig : DockerConfig, IEquatable<DockerReportedConfig>
    {
        public static DockerReportedConfig Unknown = new DockerReportedConfig(CoreConstants.Unknown, string.Empty, string.Empty);

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

        [JsonProperty(PropertyName = "imageHash")]
        public string ImageHash { get; }

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

                var options = JsonConvert.SerializeObject(dockerReportedConfig.CreateOptions)
                    .Chunks(Constants.TwinValueMaxSize)
                    .Take(Constants.TwinValueMaxChunks)
                    .Enumerate();
                foreach (var (i, chunk) in options)
                {
                    var field = i != 0
                        ? string.Format("createOptions{0}", i.ToString("D2"))
                        : "createOptions";
                    writer.WritePropertyName(field);
                    writer.WriteValue(chunk);
                }

                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject obj = JObject.Load(reader);

                // Pull out JToken values from json
                obj.TryGetValue("image", StringComparison.OrdinalIgnoreCase, out JToken jTokenImage);
                obj.TryGetValue("imageHash", StringComparison.OrdinalIgnoreCase, out JToken jTokenImageHash);

                var options = obj.ChunkedValue("createOptions", true)
                    .Take(Constants.TwinValueMaxChunks)
                    .Select(token => token?.ToString() ?? string.Empty)
                    .Join();

                return new DockerReportedConfig(jTokenImage?.ToString(), options, jTokenImageHash?.ToString());
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(DockerReportedConfig);
        }
    }
}
