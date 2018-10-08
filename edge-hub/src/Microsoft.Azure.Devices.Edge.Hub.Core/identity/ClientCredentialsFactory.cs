// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
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
        readonly string callerProductInfo;

        readonly string iotHubHostName;

        public ClientCredentialsFactory(string iotHubHostName)
            : this(iotHubHostName, string.Empty)
        {
        }

        public ClientCredentialsFactory(string iotHubHostName, string callerProductInfo)
        {
            this.iotHubHostName = iotHubHostName;
            this.callerProductInfo = callerProductInfo;
        }

        public IClientCredentials GetWithConnectionString(string connectionString)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(connectionString);
            IIdentity identity = this.GetIdentity(iotHubConnectionStringBuilder.DeviceId, iotHubConnectionStringBuilder.ModuleId);
            return new SharedKeyCredentials(identity, connectionString, this.callerProductInfo);
        }

        public IClientCredentials GetWithIotEdged(string deviceId, string moduleId)
        {
            return new IotEdgedCredentials(this.GetIdentity(deviceId, moduleId), this.callerProductInfo);
        }

        public IClientCredentials GetWithSasToken(string deviceId, string moduleId, string deviceClientType, string token)
        {
            string productInfo = string.Join(" ", this.callerProductInfo, deviceClientType).Trim();
            IIdentity identity = this.GetIdentity(deviceId, moduleId);
            return new TokenCredentials(identity, token, productInfo);
        }

        public IClientCredentials GetWithX509Cert(string deviceId, string moduleId, string deviceClientType)
        {
            string productInfo = string.Join(" ", this.callerProductInfo, deviceClientType).Trim();
            IIdentity identity = this.GetIdentity(deviceId, moduleId);
            return new X509CertCredentials(identity, productInfo);
        }

        IIdentity GetIdentity(string deviceId, string moduleId)
        {
            IIdentity identity = string.IsNullOrWhiteSpace(moduleId)
                ? new DeviceIdentity(this.iotHubHostName, deviceId)
                : new ModuleIdentity(this.iotHubHostName, deviceId, moduleId) as IIdentity;
            return identity;
        }
    }
}
