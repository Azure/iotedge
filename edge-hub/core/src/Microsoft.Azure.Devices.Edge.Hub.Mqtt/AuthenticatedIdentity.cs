// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    public class AuthenticatedIdentity : IDeviceIdentity
    {
        public AuthenticatedIdentity(string id)
        {
            this.Id = Preconditions.CheckNotNull(id, nameof(id));
        }

        public bool IsAuthenticated => true;
        public string Id { get; }
    }
}
