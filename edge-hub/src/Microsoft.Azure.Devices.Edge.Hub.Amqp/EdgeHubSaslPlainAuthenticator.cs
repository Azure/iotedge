// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Security.Principal;
    using System.Threading.Tasks;

    using Microsoft.Azure.Amqp.Sasl;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class EdgeHubSaslPlainAuthenticator : ISaslPlainAuthenticator
    {
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly string iotHubHostName;

        public EdgeHubSaslPlainAuthenticator(IAuthenticator authenticator, IClientCredentialsFactory clientCredentialsFactory, string iotHubHostName)
        {
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
        }

        public async Task<IPrincipal> AuthenticateAsync(string identity, string password)
        {
            try
            {
                Preconditions.CheckNonWhiteSpace(identity, nameof(identity));
                Preconditions.CheckNonWhiteSpace(password, nameof(password));

                (string deviceId, string moduleId, string iotHubName) = SaslIdentity.Parse(identity);

                // we MUST have a device ID
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    throw new EdgeHubConnectionException("Identity does not contain device ID.");
                }

                if (!this.iotHubHostName.Equals(iotHubName))
                {
                    throw new EdgeHubConnectionException($"Identity contains an invalid IotHubHostName {iotHubName}, expected value {this.iotHubHostName}.");
                }

                // TODO: Figure out where the device client type parameter value should come from.
                IClientCredentials deviceIdentity = this.clientCredentialsFactory.GetWithSasToken(deviceId, moduleId, string.Empty, password);

                if (!await this.authenticator.AuthenticateAsync(deviceIdentity))
                {
                    throw new EdgeHubConnectionException("Authentication failed.");
                }

                return new SaslPrincipal(new AmqpAuthentication(true, Option.Some(deviceIdentity)));
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.AuthenticationError(ex);
                throw;
            }
        }

        static class Events
        {
            const int IdStart = AmqpEventIds.SaslPlainAuthenticator;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubSaslPlainAuthenticator>();

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
