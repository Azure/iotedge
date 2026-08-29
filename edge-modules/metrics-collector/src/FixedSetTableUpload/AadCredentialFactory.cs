// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    // Builds the TokenCredential used to authenticate to the Logs Ingestion API.
    // Client secrets are intentionally not supported: use a certificate-based app
    // registration, or rely on ambient credentials (workload identity federated
    // token, Azure CLI login, managed identity, etc.) picked up by DefaultAzureCredential.
    internal static class AadCredentialFactory
    {
        public static TokenCredential Create(Settings settings)
        {
            Uri authorityHost = GetAuthorityHost(settings.AzureDomain);

            if (!string.IsNullOrWhiteSpace(settings.AadClientCertificatePath))
            {
                string tenantId = Preconditions.CheckNonWhiteSpace(settings.AadTenantId, nameof(settings.AadTenantId));
                string clientId = Preconditions.CheckNonWhiteSpace(settings.AadClientId, nameof(settings.AadClientId));

                var options = new ClientCertificateCredentialOptions { AuthorityHost = authorityHost };
                if (string.IsNullOrWhiteSpace(settings.AadClientCertificatePassword))
                {
                    return new ClientCertificateCredential(tenantId, clientId, settings.AadClientCertificatePath, options);
                }

                var certificate = new X509Certificate2(settings.AadClientCertificatePath, settings.AadClientCertificatePassword);
                return new ClientCertificateCredential(tenantId, clientId, certificate, options);
            }

            var defaultOptions = new DefaultAzureCredentialOptions { AuthorityHost = authorityHost };
            if (!string.IsNullOrWhiteSpace(settings.AadTenantId))
            {
                defaultOptions.TenantId = settings.AadTenantId;
            }

            if (!string.IsNullOrWhiteSpace(settings.AadClientId))
            {
                defaultOptions.ManagedIdentityClientId = settings.AadClientId;
                defaultOptions.WorkloadIdentityClientId = settings.AadClientId;
            }

            return new DefaultAzureCredential(defaultOptions);
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
