// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    public sealed class AzureActiveDirectory
    {
        readonly string azureActiveDirTenant = null;
        readonly string azureActiveDirClientId = null;
        readonly string azureActiveDirClientSecret = null;
        readonly DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        string azureResource = null;
        string accessToken = null;
        DateTime accessTokenExpiration = DateTime.MinValue;
        static SemaphoreSlim mutexSlim = new SemaphoreSlim(1, 1);

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
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureResource, nameof(azureResource));

            await mutexSlim.WaitAsync();
            try
            {
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

                    JObject responseJson = JObject.Parse(responseMsg);
                    this.azureResource = azureResource;
                    this.accessTokenExpiration = epochStart.AddSeconds((double)responseJson["expires_on"]);
                    this.accessToken = (string)responseJson["access_token"];
                }

                return this.accessToken;
            }
            finally
            {
                mutexSlim.Release();
            }
        }

        public bool IsAccessTokenExpired()
        {
            // Add 2 seconds as a buffer so the token does not immediately expire after the function checked.
            return DateTime.UtcNow.AddSeconds(2) > this.accessTokenExpiration;
        }

        public bool IsAccessTokenNeededAnUpdate(string azureResource)
        {
            return this.IsAccessTokenExpired() ||
                   (this.accessToken == null) ||
                   (!this.azureResource.Equals(azureResource, StringComparison.OrdinalIgnoreCase));
        }
    }
}
