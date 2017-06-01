// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [JsonConverter(typeof(DockerConfigJsonConverter))]
    // TODO add PortBindings to equality check
    public class DockerConfig : IEquatable<DockerConfig>
    {
        [JsonProperty(Required = Required.Always, PropertyName = "image")]
        public string Image { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "tag")]
        public string Tag { get; }

        [JsonProperty(Required =  Required.AllowNull, PropertyName = "portbindings")]
        public ISet<PortBinding> PortBindings { get; }

        public DockerConfig(string image, string tag)
            : this(image, tag, ImmutableHashSet<PortBinding>.Empty)
        {
        }

        [JsonConstructor]
        public DockerConfig(string image, string tag, IEnumerable<PortBinding> portBindings)
        {
            this.Image = Preconditions.CheckNotNull(image, nameof(image));
            this.Tag = Preconditions.CheckNotNull(tag, nameof(tag));
            this.PortBindings = portBindings?.ToImmutableHashSet() ?? ImmutableHashSet<PortBinding>.Empty;
        }

        public override bool Equals(object obj) => this.Equals(obj as DockerConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.Image != null ? this.Image.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Tag != null ? this.Tag.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.PortBindings != null ? this.PortBindings.GetHashCode() : 0);
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
                   string.Equals(this.Tag, other.Tag) && 
                   this.PortBindings.SetEquals(other.PortBindings);
        }

        class DockerConfigJsonConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();

                var dockerconfig = (DockerConfig)value;

                writer.WritePropertyName("image");
                serializer.Serialize(writer, dockerconfig.Image);

                writer.WritePropertyName("tag");
                serializer.Serialize(writer, dockerconfig.Tag);

                var portBindings = new Dictionary<string, PortBinding>();

                foreach (PortBinding portBinding in dockerconfig.PortBindings)
                {
                    portBindings.Add($"{portBinding.From}/{portBinding.Type.ToString().ToLower()}", portBinding);
                }

                if (portBindings.Count != 0)
                {
                    writer.WritePropertyName("portbindings");
                    serializer.Serialize(writer, portBindings);
                }

                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject obj = JObject.Load(reader);

                string dockerImage = JsonEx.Get<string>(obj, "image");

                string dockerTag = JsonEx.Get<string>(obj, "tag");

                var portBindings = new List<PortBinding>();
                //portbindings is option in our JSON. So, just fill portBindings List if there are values.
                if (obj.TryGetValue("portbindings", StringComparison.OrdinalIgnoreCase, out JToken dockerPortbindings))
                {
                    foreach (JToken portBindingValue in dockerPortbindings.Values())
                    {
                        portBindings.Add(JsonConvert.DeserializeObject<PortBinding>(portBindingValue.ToString()));
                    }
                }

                return new DockerConfig(dockerImage, dockerTag, portBindings); 
            }

       

            public override bool CanConvert(Type objectType) => objectType == typeof(DockerConfig);
        }
    }
}