// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class IdentityAndAuthChain
    {
        public IdentityAndAuthChain(string identity, Option<string> authChain)
        {
            this.Identity = Preconditions.CheckNonWhiteSpace(identity, nameof(identity));
            this.AuthChain = authChain;
        }

        [JsonProperty("Identity")]
        public string Identity { get; }

        [JsonProperty("AuthChain")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> AuthChain { get; }
    }
}
