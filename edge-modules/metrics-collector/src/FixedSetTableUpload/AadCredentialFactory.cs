// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload
{
    using System;
    using global::Azure.Core;
    using global::Azure.Identity;

    // Builds the TokenCredential used to authenticate to the Logs Ingestion API.
    // Authentication is configured through the standard AZURE_* environment variables
    // that DefaultAzureCredential reads; see the module README for details.
    internal static class AadCredentialFactory
    {
        public static TokenCredential Create(Settings settings)
        {
            var options = new DefaultAzureCredentialOptions
            {
                AuthorityHost = GetAuthorityHost(settings.AzureDomain)
            };

            return new DefaultAzureCredential(options);
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
