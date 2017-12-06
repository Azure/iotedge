// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

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
            Try<IIdentity> deviceIdentity = this.identityFactory.GetWithSasToken(username, password);
            if (!deviceIdentity.Success
                || !clientId.Equals(deviceIdentity.Value.Id, StringComparison.Ordinal)
                || !await this.authenticator.AuthenticateAsync(deviceIdentity.Value))
            {
                Events.Error(clientId, username);
                return UnauthenticatedDeviceIdentity.Instance;
            }
            Events.Success(clientId, username);
            return new ProtocolGatewayIdentity(deviceIdentity.Value);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<SasTokenDeviceIdentityProvider>();
            const int IdStart = MqttEventIds.SasTokenDeviceIdentityProvider;

            enum EventIds
            {
                CreateSuccess = IdStart,
                CreateFailure
            }

            public static void Success(string clientId, string username)
            {
                Log.LogInformation((int)EventIds.CreateSuccess, Invariant($"Successfully generated identity for clientId {clientId} and username {username}"));
            }

            public static void Error(string clientId, string username)
            {
                Log.LogError((int)EventIds.CreateFailure, Invariant($"Unable to generate identity for clientId {clientId} and username {username}"));
            }
        }
    }
}
