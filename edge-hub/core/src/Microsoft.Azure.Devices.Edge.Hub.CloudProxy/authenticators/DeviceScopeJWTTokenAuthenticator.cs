// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Extensions.Logging;

    public class DeviceScopeJWTTokenAuthenticator : DeviceScopeAuthenticator<ITokenCredentials>
    {
        readonly string iothubHostName;
        readonly string edgeHubHostName;
        private WorkloadClient workloadClient;

        public DeviceScopeJWTTokenAuthenticator(
            WorkloadClient workloadClient,
            string iothubHostName,
            string edgeHubHostName,
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            IAuthenticator underlyingAuthenticator,
            bool allowDeviceAuthForModule,
            bool syncServiceIdentityOnFailure,
            bool nestedEdgeEnabled = true)
            : base(deviceScopeIdentitiesCache, underlyingAuthenticator, allowDeviceAuthForModule, syncServiceIdentityOnFailure, nestedEdgeEnabled)
        {
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.edgeHubHostName = Preconditions.CheckNotNull(edgeHubHostName, nameof(edgeHubHostName));
            this.workloadClient = Preconditions.CheckNotNull(workloadClient, nameof(workloadClient));
        }

        protected override bool AreInputCredentialsValid(ITokenCredentials credentials) => true;

        protected override bool ValidateWithServiceIdentity(ServiceIdentity serviceIdentity, ITokenCredentials credentials)
        {
            return this.TryGetSharedAccessSignature(credentials.Token, credentials.Identity, out string sharedAccessSignature)
                ? this.ValidateCredentials(sharedAccessSignature, serviceIdentity, credentials.Identity)
               : false;
        }

        protected override async Task<bool> ValidateWithWorkloadAPI(ITokenCredentials credentials)
        {
            var generatedToken = await this.workloadClient.ValidateTokenAsync(credentials.Token);
            return credentials.Token == generatedToken;
        }

        bool TryGetSharedAccessSignature(string token, IIdentity identity, out string sharedAccessSignature)
        {
            bool isTokenGood = false;

            sharedAccessSignature = "";
            try
            {
                var generatedToken = this.workloadClient.ValidateTokenAsync(token).Result;
                isTokenGood = token == generatedToken;
#pragma warning disable IDE0059 // Unnecessary assignment of a value
#pragma warning disable CS0168 // Variable is declared but never used
            } catch( Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            {
                isTokenGood = false;
            }
            
            return isTokenGood;
        }

        bool ValidateCredentials(string sharedAccessSignature, ServiceIdentity serviceIdentity, IIdentity identity) => true;
    }
}
