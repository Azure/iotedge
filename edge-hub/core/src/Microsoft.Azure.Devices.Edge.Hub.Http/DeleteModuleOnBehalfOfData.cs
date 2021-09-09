// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using Newtonsoft.Json;

    public class DeleteModuleOnBehalfOfData
    {
        public DeleteModuleOnBehalfOfData(string authChian, string moduleId)
        {
            this.AuthChain = authChian;
            this.ModuleId = moduleId;
        }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }

        [JsonProperty(PropertyName = "moduleId", Required = Required.Always)]
        public string ModuleId { get; private set; }
    }
}
