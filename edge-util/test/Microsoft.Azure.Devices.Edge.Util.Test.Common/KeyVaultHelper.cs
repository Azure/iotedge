// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    /// <summary>
    /// We should eventually replace this with the KeyVault Configuration provider.
    /// However, currently it doesn't seem to support access using Certificate (it only supports Client secret)
    /// https://docs.microsoft.com/en-us/aspnet/core/security/key-vault-configuration
    /// </summary>
    public class KeyVaultHelper
    {
        readonly string clientId;
        readonly X509Certificate2 clientAssertionCert;

        Option<IClientAssertionCertificate> assertionCert = Option.None<IClientAssertionCertificate>();
        Option<KeyVaultClient> keyVaultClient = Option.None<KeyVaultClient>();

        public KeyVaultHelper(string clientId, X509Certificate2 clientAssertionCert)
        {
            Preconditions.CheckNonWhiteSpace(clientId, nameof(clientId));
            Preconditions.CheckNotNull(clientAssertionCert, nameof(clientAssertionCert));
            this.clientId = clientId;
            this.clientAssertionCert = clientAssertionCert;            
        }

        public async Task<string> GetSecret(string secretUrl)
        {
            Preconditions.CheckNonWhiteSpace(secretUrl, nameof(secretUrl));
            KeyVaultClient keyVault = this.GetKeyVaultClient();
            SecretBundle secretBundle = await keyVault.GetSecretAsync(secretUrl);
            return secretBundle.Value;
        }

        public async Task<string> GetSecret(string vaultBaseUrl, string secretName)
        {
            Preconditions.CheckNonWhiteSpace(vaultBaseUrl, nameof(vaultBaseUrl));
            Preconditions.CheckNonWhiteSpace(secretName, nameof(secretName));

            KeyVaultClient keyVault = this.GetKeyVaultClient();
            SecretBundle secretBundle = await keyVault.GetSecretAsync(vaultBaseUrl, secretName);
            return secretBundle.Value;
        }

        KeyVaultClient GetKeyVaultClient()
        {
            if (!this.keyVaultClient.HasValue)
            {
                this.keyVaultClient = Option.Some(new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(this.GetAccessToken)));
            }
            return this.keyVaultClient.OrDefault();
        }

        IClientAssertionCertificate GetClientAssertionCertificate()
        {
            if (!this.assertionCert.HasValue)
            {
                this.assertionCert = Option.Some(GetAssertionCert(this.clientId, this.clientAssertionCert));
            }
            return this.assertionCert.OrDefault();
        }                

        static IClientAssertionCertificate GetAssertionCert(string clientId, X509Certificate2 clientAssertionCert)
        {
            var assertionCert = new ClientAssertionCertificate(clientId, clientAssertionCert);
            return assertionCert;
        }

        async Task<string> GetAccessToken(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            AuthenticationResult result = await context.AcquireTokenAsync(resource, this.GetClientAssertionCertificate());
            return result.AccessToken;
        }
    }
}
