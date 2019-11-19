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
        string azureActiveDirTenant = null;
        string azureActiveDirClientId = null;
        string azureActiveDirClientSecret = null;

        string azureResource = null;
        string accessToken = null;
        DateTime accessTokenExpiration = new DateTime(DateTime.MinValue.Ticks);
        readonly object locker = new object();

        public AzureActiveDirectory(string azureActiveDirTenant,
            string azureActiveDirClientId,
            string azureActiveDirClientSecret)
        {
            this.azureActiveDirTenant = azureActiveDirTenant;
            this.azureActiveDirClientId = azureActiveDirClientId;
            this.azureActiveDirClientSecret = azureActiveDirClientSecret;
        }

        // GetAccessToken(4) is required to be called before invoking GetAccessToken(0), or GetAccessToken(1)
        // Trigger Azure Active Directory (AAD) for an OAuth2 client credential for an azure resource access.
        // API reference: https://dev.loganalytics.io/documentation/Authorization/OAuth2
        public async Task<string> GetAccessToken(string azureResource)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirTenant, nameof(azureActiveDirTenant));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirClientId, nameof(azureActiveDirClientId));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirClientSecret, nameof(azureActiveDirClientSecret));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureResource, nameof(azureResource));

            try
            {
                
                if (this.IsAccessTokenExpired() ||
                    (this.accessToken == null) ||
                    (!this.azureResource.Equals(azureResource, StringComparison.OrdinalIgnoreCase)))
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
                        var responseJson = JObject.Parse(responseMsg);
                        this.azureResource = azureResource;
                        this.accessTokenExpiration = DateTime.UtcNow.AddSeconds((double)responseJson["expires_on"] - 1);
                        this.accessToken = (string)responseJson["access_token"];
                    }
                }

                return this.accessToken;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public bool IsAccessTokenExpired()
        {
            return DateTime.Compare(DateTime.UtcNow, this.accessTokenExpiration) >= 0;
        }
    }
}
