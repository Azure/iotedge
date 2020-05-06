// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /* Sample code from:
    /* https://github.com/veyalla/MetricsCollector/blob/master/modules/MetricsCollector/AzureLogAnalytics.cs
    /* https://dejanstojanovic.net/aspnet/2018/february/send-data-to-azure-log-analytics-from-c-code/
    */

    public sealed class AzureLogAnalytics
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<AzureLogAnalytics>();
        static readonly AzureLogAnalytics instance = new AzureLogAnalytics();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static AzureLogAnalytics()
        {
        }

        AzureLogAnalytics()
        {
        }

        public static AzureLogAnalytics Instance
        {
            get
            {
                return instance;
            }
        }

        public async Task PostAsync(string workspaceId, string sharedKey, string content, string logType)
        {
            Preconditions.CheckNotNull(workspaceId, "Log Analytics workspace ID cannot be empty.");
            Preconditions.CheckNotNull(sharedKey, "Log Analytics shared key cannot be empty.");
            Preconditions.CheckNotNull(content, "Log Analytics content cannot be empty.");
            Preconditions.CheckNotNull(logType, "Log Analytics log type cannot be empty.");

            const string apiVersion = "2016-04-01";

            try
            {
                string dateString = DateTime.UtcNow.ToString("r");
                Uri requestUri = new Uri($"https://{workspaceId}.ods.opinsights.azure.com/api/logs?api-version={apiVersion}");
                string signature = this.GetSignature("POST", content.Length, "application/json", dateString, "/api/logs", workspaceId, sharedKey);

                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", signature);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", logType);
                client.DefaultRequestHeaders.Add("x-ms-date", dateString);

                var contentMsg = new StringContent(content, Encoding.UTF8);
                contentMsg.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Log.LogDebug(
                    client.DefaultRequestHeaders.ToString() +
                    contentMsg.Headers +
                    contentMsg.ReadAsStringAsync().Result);

                var response = await client.PostAsync(requestUri, contentMsg).ConfigureAwait(false);
                var responseMsg = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.LogDebug(
                    ((int)response.StatusCode).ToString() + " " +
                    response.ReasonPhrase + " " +
                    responseMsg);
            }
            catch (Exception e)
            {
                Log.LogError(e.Message);
            }
        }

        private string GetSignature(string method, int contentLength, string contentType, string date, string resource, string workspaceId, string sharedKey)
        {
            string message = $"{method}\n{contentLength}\n{contentType}\nx-ms-date:{date}\n{resource}";
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            using (HMACSHA256 encryptor = new HMACSHA256(Convert.FromBase64String(sharedKey)))
            {
                return $"SharedKey {workspaceId}:{Convert.ToBase64String(encryptor.ComputeHash(bytes))}";
            }
        }
    }
}