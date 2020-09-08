// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class DeviceIdentityProvider : IDeviceIdentityProvider
    {
        readonly IAuthenticator authenticator;
        readonly IUsernameParser usernameParser;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly bool clientCertAuthAllowed;
        readonly IMetadataStore metadataStore;
        Option<X509Certificate2> remoteCertificate;
        IList<X509Certificate2> remoteCertificateChain;

        public DeviceIdentityProvider(
            IAuthenticator authenticator,
            IUsernameParser usernameParser,
            IClientCredentialsFactory clientCredentialsFactory,
            IMetadataStore metadataStore,
            bool clientCertAuthAllowed)
        {
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.usernameParser = Preconditions.CheckNotNull(usernameParser, nameof(usernameParser));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.metadataStore = Preconditions.CheckNotNull(metadataStore, nameof(metadataStore));
            this.clientCertAuthAllowed = clientCertAuthAllowed;
            this.remoteCertificate = Option.None<X509Certificate2>();
            this.remoteCertificateChain = new List<X509Certificate2>();
        }

        public async Task<IDeviceIdentity> GetAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            try
            {
                Preconditions.CheckNonWhiteSpace(username, nameof(username));
                Preconditions.CheckNonWhiteSpace(clientId, nameof(clientId));

                ClientInfo clientInfo = this.usernameParser.Parse(username);
                clientInfo.ModelId.ForEach(async m => await this.metadataStore.SetModelId(clientInfo.DeviceId, m));
                IClientCredentials deviceCredentials = null;

                if (!string.IsNullOrEmpty(password))
                {
                    deviceCredentials = this.clientCredentialsFactory.GetWithSasToken(clientInfo.DeviceId, clientInfo.ModuleId, clientInfo.DeviceClientType, password, false, clientInfo.ModelId);
                }
                else if (this.remoteCertificate.HasValue)
                {
                    if (!this.clientCertAuthAllowed)
                    {
                        Events.CertAuthNotEnabled(clientInfo.DeviceId, clientInfo.ModuleId);
                        return UnauthenticatedDeviceIdentity.Instance;
                    }

                    this.remoteCertificate.ForEach(
                        cert =>
                        {
                            deviceCredentials = this.clientCredentialsFactory.GetWithX509Cert(
                                clientInfo.DeviceId,
                                clientInfo.ModuleId,
                                clientInfo.DeviceClientType,
                                cert,
                                this.remoteCertificateChain,
                                clientInfo.ModelId);
                        });
                }
                else
                {
                    Events.AuthNotFound(clientInfo.DeviceId, clientInfo.ModuleId);
                    return UnauthenticatedDeviceIdentity.Instance;
                }

                if (deviceCredentials == null
                    || !clientId.Equals(deviceCredentials.Identity.Id, StringComparison.Ordinal)
                    || !await this.authenticator.AuthenticateAsync(deviceCredentials))
                {
                    Events.Error(clientId, username);
                    return UnauthenticatedDeviceIdentity.Instance;
                }

                await this.metadataStore.SetMetadata(deviceCredentials.Identity.Id, clientInfo.DeviceClientType, clientInfo.ModelId);
                Events.Success(clientId, username);
                return new ProtocolGatewayIdentity(deviceCredentials, clientInfo.ModelId);
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingIdentity(ex);
                throw;
            }
        }

        public void RegisterConnectionCertificate(X509Certificate2 certificate, IList<X509Certificate2> chain)
        {
            this.remoteCertificate = Option.Some(Preconditions.CheckNotNull(certificate, nameof(certificate)));
            this.remoteCertificateChain = Preconditions.CheckNotNull(chain, nameof(chain));
        }

        static class Events
        {
            const int IdStart = MqttEventIds.SasTokenDeviceIdentityProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceIdentityProvider>();

            enum EventIds
            {
                CreateSuccess = IdStart,
                CreateFailure,
                CertAuthNotEnabled,
                AuthNotFound,
                ErrorCreatingIdentity
            }

            public static void Success(string clientId, string username)
                => Log.LogInformation((int)EventIds.CreateSuccess, Invariant($"Successfully generated identity for clientId {clientId} and username {username}"));

            public static void Error(string clientId, string username)
                => Log.LogError((int)EventIds.CreateFailure, Invariant($"Unable to generate identity for clientId {clientId} and username {username}"));

            public static void CertAuthNotEnabled(string deviceId, string moduleId)
                => Log.LogInformation((int)EventIds.CertAuthNotEnabled, Invariant($"Cannot create identity for {GetId(deviceId, moduleId)} because certificate authentication is not enabled"));

            public static void AuthNotFound(string deviceId, string moduleId)
                => Log.LogInformation((int)EventIds.AuthNotFound, Invariant($"Cannot create identity for {GetId(deviceId, moduleId)} because neither token nor certificate was presented"));

            public static void ErrorCreatingIdentity(Exception ex)
                => Log.LogError((int)EventIds.ErrorCreatingIdentity, ex, "Error creating client identity");

            static string GetId(string deviceId, string moduleId) =>
                string.IsNullOrWhiteSpace(moduleId) ? deviceId : $"{deviceId}/{moduleId}";
        }
    }
}
