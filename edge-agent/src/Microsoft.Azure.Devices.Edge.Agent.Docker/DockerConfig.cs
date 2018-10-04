// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Linq;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [JsonConverter(typeof(DockerConfigJsonConverter))]
    public class DockerConfig : IEquatable<DockerConfig>
    {
        readonly CreateContainerParameters createOptions;

        public string Image { get; }

        // Do a serialization roundtrip to clone the createOptions
        // https://docs.docker.com/engine/api/v1.25/#operation/ContainerCreate
        public CreateContainerParameters CreateOptions => JsonConvert.DeserializeObject<CreateContainerParameters>(JsonConvert.SerializeObject(this.createOptions));

        public DockerConfig(string image)
            : this(image, string.Empty)
        {
        }

        public DockerConfig(string image, string createOptions)
        {
            this.Image = image?.Trim() ?? string.Empty;
            this.createOptions = string.IsNullOrWhiteSpace(createOptions)
                ? new CreateContainerParameters()
                : JsonConvert.DeserializeObject<CreateContainerParameters>(createOptions);
        }

        public DockerConfig(string image, CreateContainerParameters createOptions)
        {
            this.Image = image?.Trim() ?? string.Empty;
            this.createOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
        }

        public override bool Equals(object obj) => this.Equals(obj as DockerConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.Image != null ? this.Image.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.createOptions?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        public bool Equals(DockerConfig other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.Image, other.Image) &&
                CompareCreateOptions(this.CreateOptions, other.CreateOptions);
        }

        class DockerConfigJsonConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();

                var dockerconfig = (DockerConfig)value;

                writer.WritePropertyName("image");
                serializer.Serialize(writer, dockerconfig.Image);

                var options = JsonConvert.SerializeObject(dockerconfig.CreateOptions);
                foreach (var (i, chunk) in options.Chunks(Constants.TwinMaxValueSize).Enumerate())
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

                var options = obj.ChunkedValue("createOptions", true)
                    .Select(token => token?.ToString() ?? string.Empty)
                    .Join("");

                return new DockerConfig(jTokenImage?.ToString(), options);
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(DockerConfig);
        }

        static bool CompareCreateOptions(CreateContainerParameters a, CreateContainerParameters b)
        {
            bool result;

            if ((a != null) && (b != null))
            {
                string aValue = null;
                string bValue = null;

                // Remove the `normalizedCreateOptions` labels from comparison consideration
                if (a.Labels?.TryGetValue("normalizedCreateOptions", out aValue) ?? false)
                {
                    a.Labels?.Remove("normalizedCreateOptions");
                }
                if (b.Labels?.TryGetValue("normalizedCreateOptions", out bValue) ?? false)
                {
                    b.Labels?.Remove("normalizedCreateOptions");
                }

                result = JsonConvert.SerializeObject(a).Equals(JsonConvert.SerializeObject(b));

                // Restore `normalizedCreateOptions` labels
                if (aValue != null)
                {
                    a.Labels.Add("normalizedCreateOptions", aValue);
                }
                if (bValue != null)
                {
                    b.Labels.Add("normalizedCreateOptions", bValue);
                }
            }
            else
            {
                result = (a == b);
            }

            return result;
        }
    }
}
