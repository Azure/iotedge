// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentityProviderServiceBuilder
    {
        readonly string iotHubHostname;
        readonly string deviceId;

        public ModuleIdentityProviderServiceBuilder(string iotHubHostname, string deviceId)
        {
            this.iotHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public IModuleIdentity Create(string moduleId, string generationId, string providerUri)
        {
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));

            ICredentials credentials = new IdentityProviderServiceCredentials(providerUri, generationId);
            return new ModuleIdentity(this.iotHubHostname, this.deviceId, moduleId, credentials);
        }

        public IModuleIdentity Create(string moduleId, string generationId, string providerUri, string authScheme)
        {
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));
            Preconditions.CheckNonWhiteSpace(authScheme, nameof(authScheme));

            ICredentials credentials = new IdentityProviderServiceCredentials(providerUri, generationId, authScheme);
            return new ModuleIdentity(this.iotHubHostname, this.deviceId, moduleId, credentials);
        }
    }
}
