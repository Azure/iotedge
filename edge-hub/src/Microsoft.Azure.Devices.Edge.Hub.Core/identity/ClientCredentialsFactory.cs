// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// The <c>IdentityFactory</c> is responsible for creating <see cref="Identity"/> instances
    /// given device/module credentials. Implementations of this interface are expected to
    /// derive the right kind of identity instance (<see cref="DeviceIdentity"/> or <see cref="ModuleIdentity"/>)
    /// by examining the credentials.
    /// </summary>
    public class ClientCredentialsFactory : IClientCredentialsFactory
    {
        readonly IIdentityProvider identityProvider;
        readonly string callerProductInfo;

        public ClientCredentialsFactory(IIdentityProvider identityProvider)
            : this(identityProvider, string.Empty)
        {
        }

        public ClientCredentialsFactory(IIdentityProvider identityProvider, string callerProductInfo)
        {
            this.identityProvider = identityProvider;
            this.callerProductInfo = callerProductInfo;
        }

        public IClientCredentials GetWithX509Cert(string deviceId, string moduleId, string deviceClientType, X509Certificate2 clientCertificate, IList<X509Certificate2> clientChainCertificate)
        {
            string productInfo = string.Join(" ", this.callerProductInfo, deviceClientType).Trim();
            IIdentity identity = this.identityProvider.Create(deviceId, moduleId);
            return new X509CertCredentials(identity, productInfo, clientCertificate, clientChainCertificate);
        }

        public IClientCredentials GetWithSasToken(string deviceId, string moduleId, string deviceClientType, string token, bool updatable)
        {
            string productInfo = string.Join(" ", this.callerProductInfo, deviceClientType).Trim();
            IIdentity identity = this.identityProvider.Create(deviceId, moduleId);
            return new TokenCredentials(identity, token, productInfo, updatable);
        }

        public IClientCredentials GetWithConnectionString(string connectionString)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(connectionString);
            IIdentity identity = this.identityProvider.Create(iotHubConnectionStringBuilder.DeviceId, iotHubConnectionStringBuilder.ModuleId);
            return new SharedKeyCredentials(identity, connectionString, this.callerProductInfo);
        }

        public IClientCredentials GetWithIotEdged(string deviceId, string moduleId) =>
            new IotEdgedCredentials(this.identityProvider.Create(deviceId, moduleId), this.callerProductInfo);
    }
}
