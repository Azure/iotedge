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
        static readonly AzureActiveDirectory instance = new AzureActiveDirectory();

        static string azureActiveDirTenant = null;
        static string azureActiveDirClientId = null;
        static string azureActiveDirClientSecret = null;
        static string azureResource = null;
        static string accessToken = null;
        static DateTime accessTokenExpiration = new DateTime(DateTime.MinValue.Ticks);
        static readonly object locker = new object();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static AzureActiveDirectory()
        {
        }

        AzureActiveDirectory()
        {
        }

        public static AzureActiveDirectory Instance => instance;

        // Trigger Azure Active Directory (AAD) for an OAuth2 client credential for an azure resource access.
        // API reference: https://dev.loganalytics.io/documentation/Authorization/OAuth2
        public async Task<string> GetAccessToken(
            string azureActiveDirTenant,
            string azureActiveDirClientId,
            string azureActiveDirClientSecret,
            string azureResource)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirTenant, nameof(azureActiveDirTenant));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirClientId, nameof(azureActiveDirClientId));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureActiveDirClientSecret, nameof(azureActiveDirClientSecret));
            ValidationUtil.ThrowIfNullOrWhiteSpace(azureResource, nameof(azureResource));

            try
            {
                
                if (AzureActiveDirectory.IsAccessTokenExpired() ||
                    (AzureActiveDirectory.accessToken == null) ||
                    (!AzureActiveDirectory.azureResource.Equals(azureResource, StringComparison.OrdinalIgnoreCase)))
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
                        AzureActiveDirectory.azureActiveDirTenant = azureActiveDirTenant;
                        AzureActiveDirectory.azureActiveDirClientId = azureActiveDirClientId;
                        AzureActiveDirectory.azureActiveDirClientSecret = azureActiveDirClientSecret;
                        AzureActiveDirectory.azureResource = azureResource;

                        var responseJson = JObject.Parse(responseMsg);
                        AzureActiveDirectory.accessTokenExpiration = DateTime.UtcNow.AddSeconds((double)responseJson["expires_on"] - 1);
                        AzureActiveDirectory.accessToken = (string)responseJson["access_token"];
                    }
                }

                return AzureActiveDirectory.accessToken;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public Task<string> GetAccessToken() =>
            AzureActiveDirectory.Instance.GetAccessToken(
                AzureActiveDirectory.azureActiveDirTenant,
                AzureActiveDirectory.azureActiveDirClientId,
                AzureActiveDirectory.azureActiveDirClientSecret,
                AzureActiveDirectory.azureResource);

        public Task<string> GetAccessToken(string azureResource) =>
            AzureActiveDirectory.Instance.GetAccessToken(
                AzureActiveDirectory.azureActiveDirTenant,
                AzureActiveDirectory.azureActiveDirClientId,
                AzureActiveDirectory.azureActiveDirClientSecret,
                azureResource);

        static public bool IsAccessTokenExpired()
        {
            return DateTime.Compare(DateTime.UtcNow, AzureActiveDirectory.accessTokenExpiration) >= 0;
        }
    }
}
