// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    public class StringIdentity : IDeviceIdentity
    {
        public StringIdentity(string id)
        {
            this.Id = Preconditions.CheckNotNull(id, nameof(id));
        }

        public bool IsAuthenticated => true;
        public string Id { get; }
    }
}
