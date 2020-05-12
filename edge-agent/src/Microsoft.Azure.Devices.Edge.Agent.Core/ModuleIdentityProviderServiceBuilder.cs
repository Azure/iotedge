// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentityProviderServiceBuilder
    {
        readonly string iotHubHostname;
        readonly string deviceId;
        readonly string edgeDeviceHostname;
        readonly string parentEdgeHostname;

        public ModuleIdentityProviderServiceBuilder(string iotHubHostname, string deviceId, string edgeDeviceHostname, string parentEdgeHostname)
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.edgeDeviceHostname = Preconditions.CheckNonWhiteSpace(edgeDeviceHostname, nameof(edgeDeviceHostname));
            this.parentEdgeHostname = parentEdgeHostname;
        }

        public IModuleIdentity Create(string moduleId, string generationId, string providerUri)
        {
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));

            ICredentials credentials = new IdentityProviderServiceCredentials(providerUri, generationId);
            return new ModuleIdentity(this.iotHubHostname, this.edgeDeviceHostname, this.GetGatewayHostname(moduleId), this.deviceId, moduleId, credentials);
        }

        public IModuleIdentity Create(string moduleId, string generationId, string providerUri, string authScheme)
        {
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));
            Preconditions.CheckNonWhiteSpace(authScheme, nameof(authScheme));

            ICredentials credentials = new IdentityProviderServiceCredentials(providerUri, generationId, authScheme);
            return new ModuleIdentity(this.iotHubHostname, this.edgeDeviceHostname, this.GetGatewayHostname(moduleId), this.deviceId, moduleId, credentials);
        }

        string GetGatewayHostname(string moduleId)
        {
            if (moduleId.Equals(Constants.EdgeAgentModuleIdentityName, StringComparison.OrdinalIgnoreCase) ||
                moduleId.Equals(Constants.EdgeHubModuleIdentityName, StringComparison.OrdinalIgnoreCase))
            {
                return this.parentEdgeHostname;
            }

            return this.edgeDeviceHostname;
        }
    }
}
