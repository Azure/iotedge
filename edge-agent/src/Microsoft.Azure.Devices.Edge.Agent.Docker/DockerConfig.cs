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
        // Check if it's prefix is "$upstream" and replace with environment variable.
        const string ImageUpstreamRegexPattern = @"^\$upstream(?<path>:[1-9].*)";
        static readonly Regex ImageRegex = new Regex(ImageRegexPattern);
        static readonly Regex ImageUpstreamRegex = new Regex(ImageUpstreamRegexPattern);
        readonly CreateContainerParameters createOptions;

        public DockerConfig(string image)
            : this(image, string.Empty, Option.None<string>())
        {
        }

        public DockerConfig(string image, string createOptions, Option<string> digest)
            : this(ValidateAndGetImage(image), GetCreateOptions(createOptions), digest)
        {
        }

        public DockerConfig(string image, CreateContainerParameters createOptions, Option<string> digest)
        {
            this.Image = image;
            this.createOptions = Preconditions.CheckNotNull(createOptions, nameof(createOptions));
            this.Digest = digest;
        }

        public string Image { get; }

        // Do a serialization roundtrip to clone the createOptions
        // https://docs.docker.com/engine/api/v1.25/#operation/ContainerCreate
        public CreateContainerParameters CreateOptions => JsonConvert.DeserializeObject<CreateContainerParameters>(JsonConvert.SerializeObject(this.createOptions));

        [JsonProperty("digest", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Option<string> Digest { get; }

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
                   CompareCreateOptions(this.CreateOptions, other.CreateOptions) &&
                   Equals(this.Digest, other.Digest);
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
            return ValidateAndGetImage(image, new EnvironmentWrapper());
        }

        internal static string ValidateAndGetImage(string image, IEnvironmentWrapper env)
        {
            image = Preconditions.CheckNonWhiteSpace(image, nameof(image)).Trim();

            if (image[0] == '$')
            {
                Match matchHost = ImageUpstreamRegex.Match(image);
                if (matchHost.Success
                    && (matchHost.Groups["path"]?.Length > 0))
                {
                    string hostAddress = env.GetVariable(Core.Constants.GatewayHostnameVariableName).
                        Expect(() => new InvalidOperationException($"Could not find environment variable: {Core.Constants.GatewayHostnameVariableName}"));

                    image = hostAddress + matchHost.Groups["path"].Value;
                }
                else
                {
                    throw new ArgumentException($"Image {image} is not in the right format.If your intention is to use an environment variable, check the port is specified.");
                }
            }

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

                dockerconfig.Digest.ForEach(ct =>
                {
                    writer.WritePropertyName("digest");
                    serializer.Serialize(writer, ct);
                });
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

                if (obj.TryGetValue("digest", StringComparison.OrdinalIgnoreCase, out JToken jTokenDigest))
                {
                    return new DockerConfig(jTokenImage?.ToString(), options, Option.Maybe(jTokenDigest.ToObject<string>()));
                }
                else
                {
                    return new DockerConfig(jTokenImage?.ToString(), options, Option.None<string>());
                }
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(DockerConfig);
        }
    }

    internal interface IEnvironmentWrapper
    {
        Option<string> GetVariable(string variableName);
    }

    internal class EnvironmentWrapper : IEnvironmentWrapper
    {
        public Option<string> GetVariable(string variableName)
        {
            return Option.Some(Environment.GetEnvironmentVariable(variableName));
        }
    }
}
