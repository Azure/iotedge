// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    class ProtocolGatewayIdentity : IDeviceIdentity
    {
        public ProtocolGatewayIdentity(IClientCredentials clientCredentials, Option<string> modelId)
        {
            this.ClientCredentials = Preconditions.CheckNotNull(clientCredentials, nameof(clientCredentials));
            this.ModelId = modelId;
        }

        public bool IsAuthenticated => true;

        public string Id => this.ClientCredentials.Identity.Id;

        public IClientCredentials ClientCredentials { get; }

        public Option<string> ModelId { get; }

        public override string ToString() => this.Id;
    }
}
