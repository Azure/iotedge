// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ServiceProxy : IServiceProxy
    {
        readonly ISecurityScopesApiClient securityScopesApiClient;

        public ServiceProxy(ISecurityScopesApiClient securityScopesApiClient)
        {
            this.securityScopesApiClient = Preconditions.CheckNotNull(securityScopesApiClient, nameof(securityScopesApiClient));
        }

        public ISecurityScopeIdentitiesIterator GetSecurityScopeIdentitiesIterator()
        {
            return new SecurityScopeIdentitiesIterator(this.securityScopesApiClient);
        }

        class SecurityScopeIdentitiesIterator : ISecurityScopeIdentitiesIterator
        {
            readonly ISecurityScopesApiClient securityScopesApiClient;
            Option<string> continuationLink = Option.None<string>();

            public SecurityScopeIdentitiesIterator(ISecurityScopesApiClient securityScopesApiClient)
            {
                this.securityScopesApiClient = Preconditions.CheckNotNull(securityScopesApiClient, nameof(securityScopesApiClient));
                this.HasNext = true;
            }

            public async Task<IEnumerable<ServiceIdentity>> GetNext()
            {
                var serviceIdentities = new List<ServiceIdentity>();
                ScopeResult scopeResult = await this.continuationLink.Map(c => this.securityScopesApiClient.GetNext(c))
                    .GetOrElse(() => this.securityScopesApiClient.GetIdentitiesInScope());
                serviceIdentities.AddRange(scopeResult.Devices.Select(d => DeviceToServiceIdentity(d)));
                serviceIdentities.AddRange(scopeResult.Modules.Select(m => ModuleToServiceIdentity(m)));

                if (!string.IsNullOrWhiteSpace(scopeResult.ContinuationLink))
                {
                    this.continuationLink = Option.Some(scopeResult.ContinuationLink);
                    this.HasNext = true;
                }
                else
                {
                    this.HasNext = false;
                }

                return serviceIdentities;
            }

            public bool HasNext { get; private set; }

            static ServiceIdentity DeviceToServiceIdentity(Device device)
            {
                return new ServiceIdentity(
                    device.Id,
                    null,
                    device.Capabilities?.IotEdge ?? false,
                    GetServiceAuthentication(device.Authentication));
            }

            static ServiceIdentity ModuleToServiceIdentity(Module module)
            {
                return new ServiceIdentity(
                    module.Id,
                    null,
                    false,
                    GetServiceAuthentication(module.Authentication));
            }

            static ServiceAuthentication GetServiceAuthentication(AuthenticationMechanism authenticationMechanism)
            {
                switch (authenticationMechanism.Type)
                {
                    case Devices.AuthenticationType.CertificateAuthority:
                        return new ServiceAuthentication(AuthenticationType.CertificateAuthority, null, null);

                    case Devices.AuthenticationType.SelfSigned:
                        return new ServiceAuthentication(AuthenticationType.CertificateThumbprint, null,
                            new X509Thumbprint(authenticationMechanism.X509Thumbprint.PrimaryThumbprint, authenticationMechanism.X509Thumbprint.SecondaryThumbprint));

                    case Devices.AuthenticationType.Sas:
                        return new ServiceAuthentication(AuthenticationType.SasKey, new SymmetricKey(authenticationMechanism.SymmetricKey.PrimaryKey, authenticationMechanism.SymmetricKey.SecondaryKey), null);

                    default:
                        return new ServiceAuthentication(AuthenticationType.None, null, null);
                }
            }
        }
    }
}
