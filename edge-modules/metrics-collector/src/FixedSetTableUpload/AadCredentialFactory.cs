// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload
{
    using System;
    using global::Azure.Core;
    using global::Azure.Identity;
    using global::Azure.Monitor.Ingestion;

    // Builds the TokenCredential used to authenticate to the Logs Ingestion API.
    // Deliberately narrower than DefaultAzureCredential: only production-oriented
    // credential types are tried, so a developer's local Azure CLI/IDE session on
    // the host can never silently substitute for the intended identity. Configured
    // through the standard AZURE_* environment variables; see the module README.
    internal static class AadCredentialFactory
    {
        public static TokenCredential Create(Settings settings)
        {
            (Uri authorityHost, LogsIngestionAudience _) = GetCloudConfiguration(settings.AzureDomain);

            // Covers client secret and certificate-based app registrations.
            var environmentCredential = new EnvironmentCredential(new TokenCredentialOptions { AuthorityHost = authorityHost });
            var workloadIdentityCredential = new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions { AuthorityHost = authorityHost });
            var managedIdentityCredential = new ManagedIdentityCredential();

            return new ChainedTokenCredential(environmentCredential, workloadIdentityCredential, managedIdentityCredential);
        }

        internal static (Uri AuthorityHost, LogsIngestionAudience Audience) GetCloudConfiguration(string azureDomain)
        {
            switch (azureDomain)
            {
                case "azure.us":
                    return (AzureAuthorityHosts.AzureGovernment, LogsIngestionAudience.AzureGovernment);
                case "azure.cn":
                case "azure.com.cn":
                    return (AzureAuthorityHosts.AzureChina, LogsIngestionAudience.AzureChina);
                case "azure.com":
                    return (AzureAuthorityHosts.AzurePublicCloud, LogsIngestionAudience.AzurePublicCloud);
                default:
                    throw new ArgumentException($"Unsupported Azure domain: {azureDomain}", nameof(azureDomain));
            }
        }
    }
}
