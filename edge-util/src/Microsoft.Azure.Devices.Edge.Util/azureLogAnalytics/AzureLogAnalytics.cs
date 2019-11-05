// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
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
        AzureLogAnalytics(string workspaceId, string sharedKey, string apiVersion = "2016-04-01")
        {
            this.WorkspaceId = workspaceId;
            this.SharedKey = sharedKey;
            this.ApiVersion = apiVersion;
        }

        public string WorkspaceId { get; }
        public string SharedKey { get; }
        public string ApiVersion { get; }

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
            string dateString = DateTime.UtcNow.ToString("r");
            string signature = this.GetSignature("POST", Encoding.UTF8.GetBytes(content).Length, "application/json", dateString, "/api/logs");

            using (var client = new HttpClient())
            using (var requestMsg = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"https://{this.WorkspaceId}.ods.opinsights.azure.com/api/logs?api-version={this.ApiVersion}"),
                Headers = {
                    {HttpRequestHeader.Authorization.ToString(), signature},
                    {HttpRequestHeader.Accept.ToString(), "application/json"},
                    {"Log-Type",  LogType},
                    {"x-ms-date", dateString}
                },
                Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.Default, "application/json")
            })
            using (var response = await client.SendAsync(requestMsg).ConfigureAwait(false))
            {
                Console.WriteLine(requestMsg.ToString());
                Log.LogInformation(response.ToString());
                Console.WriteLine(response.ToString());
                //response.EnsureSuccessStatusCode();
            }

            //string requestUriString = $"https://{this.WorkspaceId}.ods.opinsights.azure.com/api/logs?api-version={this.ApiVersion}";
            //DateTime dateTime = DateTime.UtcNow;
            //string dateString = dateTime.ToString("r");
            //string signature = this.GetSignature("POST", content.Length, "application/json", dateString, "/api/logs");
            //HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUriString);
            //request.ContentType = "application/json";
            //request.Method = "POST";
            //request.Headers["Log-Type"] = LogType;
            //request.Headers["x-ms-date"] = dateString;
            //request.Headers["Authorization"] = signature;
            // using (Stream requestStreamAsync = request.GetRequestStream())
            // {
            //     requestStreamAsync.Write(content, 0, content.Length);
            // }

            // using (HttpWebResponse responseAsync = (HttpWebResponse)request.GetResponse())
            // {
            //     Log.LogInformation(responseAsync.StatusDescription);
            //     Console.WriteLine(responseAsync.StatusDescription);

            //     // Get the stream containing content returned by the server.
            //     // The using block ensures the stream is automatically closed.
            //     using (Stream dataStream = responseAsync.GetResponseStream())
            //     {
            //         StreamReader reader = new StreamReader(dataStream);
            //         string responseFromServer = reader.ReadToEnd();
            //         Log.LogInformation(responseFromServer);
            //         Console.WriteLine(responseFromServer);
            //     }
            // }
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