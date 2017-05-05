// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// We should replace this with the KeyVault Configuration provider eventually.
    /// However, currently it doesn't seem to support access using Certificate (it only supports Client secret)
    /// https://docs.microsoft.com/en-us/aspnet/core/security/key-vault-configuration
    /// </summary>
    public static class SecretsHelper
    {
        static readonly string KeyVaultBaseUrl = ConfigHelper.KeyVaultConfig["baseUrl"];
        static readonly Lazy<KeyVaultHelper> KeyVaultHelperLazy = new Lazy<KeyVaultHelper>(() => InitKeyVaultHelper(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Task<string> GetSecret(string secret)
        {
            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));
            return Uri.TryCreate(secret, UriKind.Absolute, out Uri secretUri)
                ? KeyVaultHelperLazy.Value.GetSecret(secretUri.AbsoluteUri)
                : KeyVaultHelperLazy.Value.GetSecret(KeyVaultBaseUrl, secret);
        }

        public static Task<string> GetSecretFromConfigKey(string configName)
        {
            string configValue = ConfigHelper.TestConfig[Preconditions.CheckNonWhiteSpace(configName, nameof(configName))];
            return GetSecret(configValue);
        }

        static KeyVaultHelper InitKeyVaultHelper()
        {
            string clientId = ConfigHelper.KeyVaultConfig["clientId"];
            string thumbprint = ConfigHelper.KeyVaultConfig["clientCertificateThumbprint"];
            X509Certificate2 certificate = CertificateHelper.GetCertificate(thumbprint, StoreName.My, StoreLocation.CurrentUser);
            return new KeyVaultHelper(clientId, certificate);
        }
    }
}
