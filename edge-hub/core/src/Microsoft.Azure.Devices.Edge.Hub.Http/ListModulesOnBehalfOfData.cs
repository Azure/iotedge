// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using Newtonsoft.Json;

    public class ListModulesOnBehalfOfData
    {
        public ListModulesOnBehalfOfData(string authChain)
        {
            this.AuthChain = authChain;
        }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }
    }
}
