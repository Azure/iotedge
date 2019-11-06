// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /* Sample code from:
    /* https://github.com/veyalla/MetricsCollector/blob/master/modules/MetricsCollector/AzureLogAnalytics.cs
    /* https://dejanstojanovic.net/aspnet/2018/february/send-data-to-azure-log-analytics-from-c-code/
    */

    public sealed class AzureLogAnalytics
    {
        static AzureLogAnalytics instance = null;
        static readonly ILogger Log = Logger.Factory.CreateLogger<AzureLogAnalytics>();
        public string WorkspaceId { get; }
        public string SharedKey { get; }
        public string ApiVersion { get; }
        AzureLogAnalytics(string workspaceId, string sharedKey, string apiVersion = "2016-04-01")
        {
            this.WorkspaceId = workspaceId;
            this.SharedKey = sharedKey;
            this.ApiVersion = apiVersion;
        }

        public static AzureLogAnalytics getInstance()
        {
            Preconditions.CheckNotNull(instance);
            return instance;
        }

        public static AzureLogAnalytics initInstance(string workspaceId, string sharedKey)
        {
            if(instance == null)
            {
                instance = new AzureLogAnalytics(workspaceId, sharedKey);
            }
            return instance;
        }

        public void Post(byte[] content, string LogType)
        {
            string requestUriString = $"https://{this.WorkspaceId}.ods.opinsights.azure.com/api/logs?api-version={this.ApiVersion}";
            DateTime dateTime = DateTime.UtcNow;
            string dateString = dateTime.ToString("r");
            string signature = this.GetSignature("POST", content.Length, "application/json", dateString, "/api/logs");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUriString);
            request.ContentType = "application/json";
            request.Method = "POST";
            request.Headers["Log-Type"] = LogType;
            request.Headers["x-ms-date"] = dateString;
            request.Headers["Authorization"] = signature;
            using (Stream requestStreamAsync = request.GetRequestStream())
            {
                requestStreamAsync.Write(content, 0, content.Length);
            }

            using (HttpWebResponse responseAsync = (HttpWebResponse)request.GetResponse())
            {
                Console.WriteLine(request.Headers.ToString());
                Console.WriteLine("content.Length: " + content.Length.ToString());

                Log.LogInformation(responseAsync.StatusDescription);
                Console.WriteLine(responseAsync.StatusDescription);

                // Get the stream containing content returned by the server.
                // The using block ensures the stream is automatically closed.
                using (Stream dataStream = responseAsync.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(dataStream);
                    string responseFromServer = reader.ReadToEnd();
                    Log.LogInformation(responseFromServer);
                    Console.WriteLine(responseFromServer);
                }
            }
        }

        public async void PostAsync(string content, string LogType)
        {
            //string dateString = DateTime.UtcNow.ToString("r");
            Uri RequestUri = new Uri($"https://{this.WorkspaceId}.ods.opinsights.azure.com/api/logs?api-version={this.ApiVersion}");
            DateTime dateTime = DateTime.UtcNow;
            string dateString = dateTime.ToString("r");
            string signature = this.GetSignature("POST", content.Length, "application/json", dateString, "/api/logs");

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", signature);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Log-Type", LogType);
            client.DefaultRequestHeaders.Add("x-ms-date", dateString);

            var contentMsg = new StringContent(content, Encoding.UTF8);
            contentMsg.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Log.LogDebug(
                client.DefaultRequestHeaders.ToString() + 
                contentMsg.Headers + 
                contentMsg.ReadAsStringAsync().Result);

            var response = await client.PostAsync(RequestUri, contentMsg).ConfigureAwait(false);
            var responseMsg = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Log.LogDebug(
                ((int)response.StatusCode).ToString() + " " 
                + response.ReasonPhrase + " " 
                + responseMsg);
        }

        private string GetSignature(string method, int contentLength, string contentType, string date, string resource)
        {
            string message = $"{method}\n{contentLength}\n{contentType}\nx-ms-date:{date}\n{resource}";
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            using (HMACSHA256 encryptor = new HMACSHA256(Convert.FromBase64String(this.SharedKey)))
            {
                return $"SharedKey {this.WorkspaceId}:{Convert.ToBase64String(encryptor.ComputeHash(bytes))}";
            }
        }
    }
}