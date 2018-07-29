// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Data;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class SecurityScopeTokenAuthenticator : IAuthenticator
    {
        readonly ISecurityScopeStore securityScopeStore;
        readonly string iothubHostName;
        readonly string edgeHubHostName;

        public SecurityScopeTokenAuthenticator(ISecurityScopeStore securityScopeStore, string iothubHostName, string edgeHubHostName)
        {
            this.securityScopeStore = securityScopeStore;
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.edgeHubHostName = Preconditions.CheckNonWhiteSpace(edgeHubHostName, nameof(edgeHubHostName));
        }

        public async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
        {
            if (!(clientCredentials is ITokenCredentials tokenCredentials))
            {
                return false;
            }

            SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(this.iothubHostName, tokenCredentials.Token);
            bool result = await this.ValidateToken(sharedAccessSignature, tokenCredentials.Identity.Id);
            if (!result && tokenCredentials.Identity is IModuleIdentity moduleIdentity)
            {
                result = await this.ValidateToken(sharedAccessSignature, moduleIdentity.DeviceId);
            }
            return result;
        }

        async Task<bool> ValidateToken(SharedAccessSignature sharedAccessSignature, string id)
        {
            Option<ServiceIdentity> serviceIdentity = await this.securityScopeStore.GetServiceIdentity(id);

            return serviceIdentity.Map(
                    s =>
                    {
                        // Validate token
                        Try<bool> result = this.ValidateTokenWithSecurityIdentity(sharedAccessSignature, s);
                        return result.Success;
                    })
                .GetOrElse(
                    () =>
                    {
                        // Log
                        return false;
                    });
        }

        Try<bool> ValidateTokenWithSecurityIdentity(SharedAccessSignature sharedAccessSignature, ServiceIdentity serviceIdentity)
        {
            var rule = new SharedAccessSignatureAuthorizationRule
            {
                PrimaryKey = serviceIdentity.Authentication.SymmetricKey.PrimaryKey,
                SecondaryKey = serviceIdentity.Authentication.SymmetricKey.SecondaryKey
            };
            try
            {
                sharedAccessSignature.Authenticate(rule);
                return Try.Success(true);
            }
            catch (UnauthorizedAccessException e)
            {
                return Try<bool>.Failure(e);
            }            
        }
    }
}
