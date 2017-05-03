// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    public class SasTokenDeviceIdentityProvider : IDeviceIdentityProvider
    {
        readonly IAuthenticator authenticator;
        readonly string iotHubHostName;

        public SasTokenDeviceIdentityProvider(IAuthenticator authenticator, string iotHubHostName)
        {
            this.authenticator = authenticator;
            this.iotHubHostName = iotHubHostName;
        }

        public async Task<IDeviceIdentity> GetAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            Try<HubDeviceIdentity> deviceIdentity = HubIdentityHelper.TryGetHubDeviceIdentityWithSasToken(username, this.iotHubHostName, password);
            if (!deviceIdentity.Success
                || !clientId.Equals(deviceIdentity.Value.Id, StringComparison.Ordinal)
                || !await this.authenticator.Authenticate(deviceIdentity.Value))
            {
                return UnauthenticatedDeviceIdentity.Instance;
            }
           
            return deviceIdentity.Value;
        }
    }
}
