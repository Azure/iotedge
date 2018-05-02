// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class IdentityProviderServiceCredentials : ICredentials
    {
        const string DefaultAuthScheme = "sasToken";

        public IdentityProviderServiceCredentials(string providerUri) : this(providerUri, DefaultAuthScheme)
        {
        }

        public IdentityProviderServiceCredentials(string providerUri, string authScheme) : this(providerUri, authScheme, Option.None<string>())
        {
        }

        public IdentityProviderServiceCredentials(string providerUri, string authScheme, Option<string> providerVersion)
        {
            this.CredentialsType = CredentialType.IdentityProviderService;
            this.ProviderUri = Preconditions.CheckNonWhiteSpace(providerUri, nameof(providerUri));
            this.AuthScheme = Preconditions.CheckNonWhiteSpace(authScheme, nameof(authScheme));
            this.Version = Preconditions.CheckNotNull(providerVersion, nameof(providerVersion));
        }

        public string ProviderUri { get; }

        public Option<string> Version { get; }

        public string AuthScheme { get; }

        public CredentialType CredentialsType { get; }
    }
}
