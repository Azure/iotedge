// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class CreateContainerParameters
    {
        [JsonProperty("Env", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> Env { get; set; }

        [JsonProperty("ExposedPorts", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, global::Docker.DotNet.Models.EmptyStruct> ExposedPorts { get; set; }

        [JsonProperty("HostConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public HostConfig HostConfig { get; set; }

        [JsonProperty("Image", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Image { get; set; }

        [JsonProperty("Labels", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IDictionary<string, string> Labels { get; set; }

        // This field is not actually part of the serialized JSON value:
        //
        // - In the Docker create-container API, it's a query string parameter named "name".
        // - In the Edgelet create-module API, it's the top-level "name" field of the request body.
        //
        // However Docker.DotNet's type CreateContainerParameters stores it as a property, annotated with its custom QueryStringParameterAttribute
        // to ensure it ends up in the querystring. So Edge Agent's code has been written to use it as a property of this type.
        //
        // As a result this model type continues to provide it.
        [JsonIgnore]
        public string Name { get; set; }

        [JsonProperty("NetworkingConfig", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public NetworkingConfig NetworkingConfig { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> OtherProperties { get; set; }
    }
}
