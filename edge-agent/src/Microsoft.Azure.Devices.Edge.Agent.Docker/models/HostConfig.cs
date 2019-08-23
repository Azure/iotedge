// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Models
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class HostConfig
    {
        [DataMember(Name = "Binds", EmitDefaultValue = false)]
        public IList<string> Binds;

        [DataMember(Name = "LogConfig", EmitDefaultValue = false)]
        public LogConfig LogConfig;

        [DataMember(Name = "Mounts", EmitDefaultValue = false)]
        public IList<Mount> Mounts;

        [DataMember(Name = "NetworkMode", EmitDefaultValue = false)]
        public string NetworkMode;

        [DataMember(Name = "PortBindings", EmitDefaultValue = false)]
        public IDictionary<string, IList<PortBinding>> PortBindings;

        [DataMember(Name = "Privileged", EmitDefaultValue = false)]
        public bool Privileged;

        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> OtherProperties;
    }
}
