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
        readonly IIdentityFactory identityFactory;

        public SasTokenDeviceIdentityProvider(IAuthenticator authenticator, IIdentityFactory identityFactory)
        {
            this.authenticator = authenticator;
            this.identityFactory = identityFactory;
        }

        public async Task<IDeviceIdentity> GetAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            Try<Identity> deviceIdentity = this.identityFactory.GetWithSasToken(username, password);
            if (!deviceIdentity.Success
                || !clientId.Equals(deviceIdentity.Value.Id, StringComparison.Ordinal)
                || !await this.authenticator.AuthenticateAsync(deviceIdentity.Value))
            {
                return UnauthenticatedDeviceIdentity.Instance;
            }
           
            return deviceIdentity.Value;
        }
    }
}
