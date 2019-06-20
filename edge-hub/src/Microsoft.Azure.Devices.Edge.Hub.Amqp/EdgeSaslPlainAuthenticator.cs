// Copyright (c) Microsoft. All rights reserved.
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

    public class EdgeSaslPlainAuthenticator : ISaslPlainAuthenticator
    {
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly string iotHubHostName;

        public EdgeSaslPlainAuthenticator(IAuthenticator authenticator, IClientCredentialsFactory clientCredentialsFactory, string iotHubHostName)
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

                // iotHubName can be a segment of the full iotHubHostName.
                // For example, if iotHubHostName = testhub1.azure-devices.net,
                // then iotHubName = testhub1 is valid.
                if (!this.iotHubHostName.StartsWith(iotHubName, StringComparison.OrdinalIgnoreCase) ||
                    this.iotHubHostName[iotHubName.Length] != '.')
                {
                    throw new EdgeHubConnectionException($"Identity contains an invalid IotHubHostName {iotHubName}.");
                }

                // TODO: Figure out where the device client type parameter value should come from.
                IClientCredentials deviceIdentity = this.clientCredentialsFactory.GetWithSasToken(deviceId, moduleId, string.Empty, password, false);

                if (!await this.authenticator.AuthenticateAsync(deviceIdentity))
                {
                    throw new EdgeHubConnectionException("Authentication failed.");
                }

                return new SaslPrincipal(true, deviceIdentity);
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
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeSaslPlainAuthenticator>();

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
