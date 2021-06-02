// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using Newtonsoft.Json;

    public class CreateOrUpdateModuleOnBehalfOfData
    {
        public CreateOrUpdateModuleOnBehalfOfData(string authChain, Module module)
        {
            this.AuthChain = authChain;
            this.Module = module;
        }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }

        [JsonProperty(PropertyName = "module", Required = Required.Always)]
        public Module Module { get; private set; }
    }
}
