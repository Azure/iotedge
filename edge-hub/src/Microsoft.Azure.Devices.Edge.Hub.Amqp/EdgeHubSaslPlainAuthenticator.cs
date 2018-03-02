// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using IPrincipal = System.Security.Principal.IPrincipal;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp.Sasl;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class EdgeHubSaslPlainAuthenticator : ISaslPlainAuthenticator
    {
        readonly IAuthenticator authenticator;
        readonly IIdentityFactory identityFactory;

        public EdgeHubSaslPlainAuthenticator(IAuthenticator authenticator, IIdentityFactory identityFactory)
        {
            this.identityFactory = Preconditions.CheckNotNull(identityFactory, nameof(identityFactory));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
        }

        public async Task<IPrincipal> AuthenticateAsync(string identity, string password)
        {
            try
            {
                Preconditions.CheckNonWhiteSpace(identity, nameof(identity));
                Preconditions.CheckNonWhiteSpace(password, nameof(password));

                SaslIdentity saslIdentity = SaslIdentity.Parse(identity);

                // we MUST have a device ID
                if(saslIdentity.DeviceId.HasValue == false)
                {
                    throw new EdgeHubConnectionException("Identity does not contain device ID.");
                }

                Try<IIdentity> deviceIdentity = this.identityFactory.GetWithSasToken(
                    saslIdentity.DeviceId.OrDefault(),
                    saslIdentity.ModuleId.OrDefault(),
                    // TODO: Figure out where the device client type parameter value should come from.
                    string.Empty,
                    saslIdentity.ModuleId.Map(_ => true).GetOrElse(false),
                    password
                );
                if (!deviceIdentity.Success || !await this.authenticator.AuthenticateAsync(deviceIdentity.Value))
                {
                    throw new EdgeHubConnectionException("Authentication failed.");
                }

                return new SaslPrincipal(saslIdentity, new AmqpAuthentication(true, Option.Some(deviceIdentity.Value)));
            }
            catch(Exception ex) when (!ex.IsFatal())
            {
                Events.AuthenticationError(ex);
                throw;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubSaslPlainAuthenticator>();
            const int IdStart = AmqpEventIds.SaslPlainAuthenticator;

            enum EventIds
            {
                AuthenticationError = IdStart,
            }

            public static void AuthenticationError(Exception ex)
            {
                Log.LogError((int)EventIds.AuthenticationError, ex, $"An error ocurred during authentication.");
            }
        }
    }
}
