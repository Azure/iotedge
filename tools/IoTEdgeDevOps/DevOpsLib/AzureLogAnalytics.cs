// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    public sealed class AzureLogAnalytics
    {
        const string apiVersion = "v1";

        // Use get-request to do a Kusto query
        // API reference: https://dev.loganalytics.io/documentation/Using-the-API/RequestFormat
        public async Task<string> GetKqlQuery(
            string logAnalyticsWorkspaceId,
            string kqlQuery)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(logAnalyticsWorkspaceId, nameof(logAnalyticsWorkspaceId));

            try
            {
                string requestUri = $"https://api.loganalytics.io/{apiVersion}/workspaces/{logAnalyticsWorkspaceId}/query?query=";
                string accessToken = await AzureActiveDirectory.Instance.GetAccessToken();

                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var response = await client.GetAsync($"{requestUri}{kqlQuery}").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var responseMsg = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return responseMsg;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
