// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static System.FormattableString;

    [JsonConverter(typeof(DockerConfigJsonConverter))]
    public class DockerConfig : IEquatable<DockerConfig>
    {
        // This is not the actual docker image regex, but a less strict version.
        const string ImageRegexPattern = @"^(?<repo>([^/]*/)*)(?<image>[^/:]+)(?<tag>:[^/:]+)?$";

        static readonly Regex ImageRegex = new Regex(ImageRegexPattern);
        readonly CreateContainerParameters createOptions;

        public DockerConfig(string image)
            : this(image, string.Empty)
        {
        }

        public DockerConfig(string image, string createOptions)
            : this(ValidateAndGetImage(image), GetCreateOptions(createOptions))
        {
        }

        public DockerConfig(string image, CreateContainerParameters createOptions)
        {
            this.Image = image;
            this.createOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
        }

        public string Image { get; }

        // Do a serialization roundtrip to clone the createOptions
        // https://docs.docker.com/engine/api/v1.25/#operation/ContainerCreate
        public CreateContainerParameters CreateOptions => JsonConvert.DeserializeObject<CreateContainerParameters>(JsonConvert.SerializeObject(this.createOptions));

        public override bool Equals(object obj) => this.Equals(obj as DockerConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Image != null ? this.Image.GetHashCode() : 0;
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

        internal static CreateContainerParameters GetCreateOptions(string createOptions)
        {
            CreateContainerParameters createContainerParameters = null;
            if (!string.IsNullOrWhiteSpace(createOptions) && !createOptions.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                createContainerParameters = JsonConvert.DeserializeObject<CreateContainerParameters>(createOptions);
            }

            return createContainerParameters ?? new CreateContainerParameters();
        }

        internal static string ValidateAndGetImage(string image)
        {
            image = Preconditions.CheckNonWhiteSpace(image, nameof(image)).Trim();
            Match match = ImageRegex.Match(image);
            if (match.Success)
            {
                if (match.Groups["tag"]?.Length > 0)
                {
                    return image;
                }
                else
                {
                    return Invariant($"{image}:{Constants.DefaultTag}");
                }
            }
            else
            {
                throw new ArgumentException($"Image {image} is not in the right format");
            }
        }

        internal static bool CompareCreateOptions(CreateContainerParameters a, CreateContainerParameters b)
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
                result = a == b;
            }

            return result;
        }

        class DockerConfigJsonConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();

                var dockerconfig = (DockerConfig)value;

                writer.WritePropertyName("image");
                serializer.Serialize(writer, dockerconfig.Image);

                var options = JsonConvert.SerializeObject(dockerconfig.CreateOptions)
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

                var options = obj.ChunkedValue("createOptions", true)
                    .Take(Constants.TwinValueMaxChunks)
                    .Select(token => token?.ToString() ?? string.Empty)
                    .Join();

                return new DockerConfig(jTokenImage?.ToString(), options);
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(DockerConfig);
        }
    }
}
