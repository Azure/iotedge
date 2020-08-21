// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using Microsoft.Azure.Devices.Edge.Util;

    class IdentityAndAuthChain
    {
        public IdentityAndAuthChain(string identity, Option<string> authChain)
        {
            this.Identity = Preconditions.CheckNonWhiteSpace(identity, nameof(identity));
            authChain.ForEach(a => this.AuthChain = a);
        }

        public string Identity { get; }

        public string AuthChain { get; }
    }
}
