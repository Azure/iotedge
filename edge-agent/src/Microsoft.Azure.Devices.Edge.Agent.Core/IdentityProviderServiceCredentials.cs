// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class IdentityProviderServiceCredentials : ICredentials
    {
        const string DefaultAuthScheme = "sasToken";

        public IdentityProviderServiceCredentials(string providerUri, string moduleGenerationId, string authScheme = DefaultAuthScheme)
            : this(providerUri, moduleGenerationId, authScheme, Option.None<string>())
        {
        }

        public IdentityProviderServiceCredentials(string providerUri, string moduleGenerationId, string authScheme, Option<string> providerVersion)
        {
            this.CredentialsType = CredentialType.IdentityProviderService;
            this.ProviderUri = Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));
            this.AuthScheme = Preconditions.CheckNonWhiteSpace(authScheme, nameof(authScheme));
            this.ModuleGenerationId = Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));
            this.Version = Preconditions.CheckNotNull(providerVersion, nameof(providerVersion));
        }

        [JsonConstructor]
        public IdentityProviderServiceCredentials(string providerUri, string moduleGenerationId, string authScheme, Option<string> providerVersion, CredentialType credentialsType)
        {
            this.CredentialsType = CredentialType.IdentityProviderService;
            this.ProviderUri = Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));
            this.AuthScheme = Preconditions.CheckNonWhiteSpace(authScheme, nameof(authScheme));
            this.ModuleGenerationId = Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));
            this.Version = Preconditions.CheckNotNull(providerVersion, nameof(providerVersion));
        }

        public string ProviderUri { get; }

        public Option<string> Version { get; }

        public string AuthScheme { get; }

        public string ModuleGenerationId { get; }

        public CredentialType CredentialsType { get; }
    }
}
