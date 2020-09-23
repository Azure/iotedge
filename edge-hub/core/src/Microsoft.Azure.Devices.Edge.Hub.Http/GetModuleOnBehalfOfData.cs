// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using Newtonsoft.Json;

    public class GetModuleOnBehalfOfData
    {
        public GetModuleOnBehalfOfData(string authChain, string moduleId)
        {
            this.AuthChain = authChain;
            this.ModuleId = moduleId;
        }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }

        [JsonProperty(PropertyName = "targetModuleId", Required = Required.Always)]
        public string ModuleId { get; private set; }
    }
}
