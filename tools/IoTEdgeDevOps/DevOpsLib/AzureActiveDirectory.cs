// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection.Metadata.Ecma335;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    public sealed class AzureActiveDirectory
    {
        readonly string azureActiveDirTenant = null;
        readonly string azureActiveDirClientId = null;
        readonly string azureActiveDirClientSecret = null;

        string azureResource = null;
        string accessToken = null;
        DateTime accessTokenExpiration = DateTime.MinValue;
        readonly object locker = new object();

        public AzureActiveDirectory(
            string azureActiveDirTenant,
            string azureActiveDirClientId,
            string azureActiveDirClientSecret)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirTenant, nameof(azureActiveDirTenant));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirClientId, nameof(azureActiveDirClientId));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirClientSecret, nameof(azureActiveDirClientSecret));

            this.azureActiveDirTenant = azureActiveDirTenant;
            this.azureActiveDirClientId = azureActiveDirClientId;
            this.azureActiveDirClientSecret = azureActiveDirClientSecret;
        }

        // Trigger Azure Active Directory (AAD) for an OAuth2 client credential for an azure resource access.
        // API reference: https://dev.loganalytics.io/documentation/Authorization/OAuth2
        public async Task<string> GetAccessToken(string azureResource)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(this.azureActiveDirTenant, nameof(this.azureActiveDirTenant));
            ValidationUtil.ThrowIfNullOrWhiteSpace(this.azureActiveDirClientId, nameof(this.azureActiveDirClientId));
            ValidationUtil.ThrowIfNullOrWhiteSpace(this.azureActiveDirClientSecret, nameof(this.azureActiveDirClientSecret));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureResource, nameof(azureResource));

            // TODO: Figure out a clean way to cache more than one azure resource's access token
            if (this.IsAccessTokenNeededAnUpdate(azureResource))
            {
                string requestUri = $"https://login.microsoftonline.com/{azureActiveDirTenant}/oauth2/token";
                const string grantType = "client_credentials";

                var client = new HttpClient();
                client.BaseAddress = new Uri(requestUri);

                var requestBody = new List<KeyValuePair<string, string>>();
                requestBody.Add(new KeyValuePair<string, string>("client_id", azureActiveDirClientId));
                requestBody.Add(new KeyValuePair<string, string>("client_secret", azureActiveDirClientSecret));
                requestBody.Add(new KeyValuePair<string, string>("grant_type", grantType));
                requestBody.Add(new KeyValuePair<string, string>("resource", azureResource));

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "");
                // By default, if FormUrlEncodedContent() is used, the "Content-Type" is set to "application/x-www-form-urlencoded"
                // which can be explicitly written : request.Content.Headers = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                request.Content = new FormUrlEncodedContent(requestBody);

                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string responseMsg = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                lock (locker)
                {
                    JObject responseJson = JObject.Parse(responseMsg);
                    this.azureResource = azureResource;
                    // (-1) second on the expiration time to prevent the access token immediately expired
                    // after this function is returned.
                    this.accessTokenExpiration = DateTime.UtcNow.AddSeconds((double)responseJson["expires_on"] - 1);
                    this.accessToken = (string)responseJson["access_token"];
                }
            }

            return this.accessToken;
        }

        public bool IsAccessTokenExpired()
        {
            return DateTime.Compare(DateTime.UtcNow, this.accessTokenExpiration) >= 0;
        }

        public bool IsAccessTokenNeededAnUpdate(string azureResource)
        {
            return this.IsAccessTokenExpired() ||
                   (this.accessToken == null) ||
                   (!this.azureResource.Equals(azureResource, StringComparison.OrdinalIgnoreCase));
        }
    }
}
