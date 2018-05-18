// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentityProviderServiceBuilder
    {
        readonly string iotHubHostName;
        readonly string deviceId;
        readonly string gatewayHostname;

        public ModuleIdentityProviderServiceBuilder(string iotHubHostName, string deviceId, string gatewayHostname)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.gatewayHostname = gatewayHostname;
        }

        public IModuleIdentity Create(string moduleId, string generationId, string providerUri)
        {
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));

            ICredentials credentials = new IdentityProviderServiceCredentials(providerUri, generationId);
            return new ModuleIdentity(this.iotHubHostName, this.gatewayHostname, this.deviceId, moduleId, credentials);
        }

        public IModuleIdentity Create(string moduleId, string generationId, string providerUri, string authScheme)
        {
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));
            Preconditions.CheckNonWhiteSpace(authScheme, nameof(authScheme));

            ICredentials credentials = new IdentityProviderServiceCredentials(providerUri, generationId, authScheme);
            return new ModuleIdentity(this.iotHubHostName, this.gatewayHostname, this.deviceId, moduleId, credentials);
        }
    }
}
