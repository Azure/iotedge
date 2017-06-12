// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Linq;

    [JsonConverter(typeof(DockerConfigJsonConverter))]
    public class DockerConfig : IEquatable<DockerConfig>
    {
        [JsonProperty(Required = Required.Always, PropertyName = "image")]
        public string Image { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "tag")]
        public string Tag { get; }

        [JsonProperty(Required =  Required.AllowNull, PropertyName = "portbindings")]
        public ISet<PortBinding> PortBindings { get; }

        [JsonProperty(Required =  Required.AllowNull, PropertyName = "env")]
        public IDictionary<string, string> Env { get; }

        public DockerConfig(string image, string tag)
            : this(image, tag, ImmutableHashSet<PortBinding>.Empty,
                ImmutableDictionary<string, string>.Empty)
        {
        }

        public DockerConfig(
            string image,
            string tag,
            IEnumerable<PortBinding> portBindings)
            : this(image, tag, portBindings, ImmutableDictionary<string, string>.Empty)
        {
        }

        public DockerConfig(
            string image,
            string tag,
            IDictionary<string, string> environmentVariables)
            : this(image, tag, ImmutableHashSet<PortBinding>.Empty, environmentVariables)
        {
        }

        [JsonConstructor]
        public DockerConfig(
            string image,
            string tag,
            IEnumerable<PortBinding> portBindings,
            IDictionary<string, string> environmentVariables)
        {
            this.Image = Preconditions.CheckNotNull(image, nameof(image));
            this.Tag = Preconditions.CheckNotNull(tag, nameof(tag));
            this.PortBindings = portBindings?.ToImmutableHashSet() ?? ImmutableHashSet<PortBinding>.Empty;
            this.Env = environmentVariables?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty;
        }

        public override bool Equals(object obj) => this.Equals(obj as DockerConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (this.Image != null ? this.Image.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.Tag.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.PortBindings?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.Env?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        bool EnvEquals(DockerConfig other)
        {
            // we consider this configuration as equal to the other one in terms
            // of environment variables if all of the env vars included in this
            // config match wih the env vars in the other; i.e., it is ok for the
            // list of env vars included in this instance to be a subset of the
            // env vars in the other instance

            // we get the list of elements in one set that are NOT in the other set;
            // if that list has any elements then set1 is not a subset of set2
            return this.Env.Except(other.Env).Any() == false;
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
                   this.PortBindings.SetEquals(other.PortBindings) &&
                   this.EnvEquals(other);
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

                writer.WritePropertyName("env");
                serializer.Serialize(writer, dockerconfig.Env);

                if(dockerconfig.PortBindings.Count > 0)
                {
                    writer.WritePropertyName("portbindings");
                    IDictionary<string, PortBinding> portBindingsMap = dockerconfig
                        .PortBindings.ToImmutableDictionary(pb => $"{pb.From}/{pb.Type.ToString().ToLower()}");
                    serializer.Serialize(writer, portBindingsMap);
                }

                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JObject obj = JObject.Load(reader);
                string image = obj.Get<string>("image");
                string tag = obj.Get<string>("tag");

                // De-serialize optional port maps.
                IList<PortBinding> portBindings = null;
                if (obj.TryGetValue("portbindings", StringComparison.OrdinalIgnoreCase, out JToken dockerPortbindings))
                {
                    portBindings = new List<PortBinding>();
                    foreach (JToken portBindingValue in dockerPortbindings.Values())
                    {
                        portBindings.Add(portBindingValue.ToObject<PortBinding>());
                    }
                }

                // De-serialize optional environment variables.
                IDictionary<string, string> env = null;
                if (obj.TryGetValue("env", StringComparison.OrdinalIgnoreCase, out JToken envMap))
                {
                    env = envMap.ToObject<IDictionary<string, string>>();
                }

                return new DockerConfig(image, tag, portBindings, env);
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(DockerConfig);
        }
    }
}