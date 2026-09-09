// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload
{
    using System;
    using global::Azure.Core;
    using global::Azure.Identity;

    // Builds the TokenCredential used to authenticate to the Logs Ingestion API.
    // Deliberately narrower than DefaultAzureCredential: only production-oriented
    // credential types are tried, so a developer's local Azure CLI/IDE session on
    // the host can never silently substitute for the intended identity. Configured
    // through the standard AZURE_* environment variables; see the module README.
    internal static class AadCredentialFactory
    {
        public static TokenCredential Create(Settings settings)
        {
            Uri authorityHost = GetAuthorityHost(settings.AzureDomain);

            // Covers client secret and certificate-based app registrations.
            var environmentCredential = new EnvironmentCredential(new TokenCredentialOptions { AuthorityHost = authorityHost });
            var workloadIdentityCredential = new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions { AuthorityHost = authorityHost });
            var managedIdentityCredential = new ManagedIdentityCredential();

            return new ChainedTokenCredential(environmentCredential, workloadIdentityCredential, managedIdentityCredential);
        }

        private static Uri GetAuthorityHost(string azureDomain)
        {
            switch (azureDomain)
            {
                case "azure.us":
                    return AzureAuthorityHosts.AzureGovernment;
                case "azure.cn":
                    return AzureAuthorityHosts.AzureChina;
                default:
                    return AzureAuthorityHosts.AzurePublicCloud;
            }
        }
    }
}
