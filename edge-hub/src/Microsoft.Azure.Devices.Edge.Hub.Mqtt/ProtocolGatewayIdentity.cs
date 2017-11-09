// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    class ProtocolGatewayIdentity : ProtocolGateway.Identity.IDeviceIdentity
    {
        public ProtocolGatewayIdentity(IIdentity identity)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
        }

        public bool IsAuthenticated => true;

        public string Id => this.Identity.Id;

        public IIdentity Identity { get; }

        public override string ToString() => this.Id;
    }
}
