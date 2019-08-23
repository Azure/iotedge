// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class CreateContainerParameters
    {
        [DataMember(Name = "Env", EmitDefaultValue = false)]
        public IList<string> Env { get; set; }

        [DataMember(Name = "ExposedPorts", EmitDefaultValue = false)]
        public IDictionary<string, global::Docker.DotNet.Models.EmptyStruct> ExposedPorts { get; set; }

        [DataMember(Name = "HostConfig", EmitDefaultValue = false)]
        public HostConfig HostConfig { get; set; }

        [DataMember(Name = "Image", EmitDefaultValue = false)]
        public string Image { get; set; }

        [DataMember(Name = "Labels", EmitDefaultValue = false)]
        public IDictionary<string, string> Labels { get; set; }

        [DataMember(Name = "Name", EmitDefaultValue = false)]
        public string Name { get; set; }

        [DataMember(Name = "NetworkingConfig", EmitDefaultValue = false)]
        public NetworkingConfig NetworkingConfig { get; set; }

        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> OtherProperties { get; set; }
    }
}
