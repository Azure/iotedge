// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics
{
    using System;
    using System.IO;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    
    // Sample code from:
    // https://github.com/veyalla/MetricsCollector/blob/master/modules/MetricsCollector/AzureLogAnalytics.cs
    // https://dejanstojanovic.net/aspnet/2018/february/send-data-to-azure-log-analytics-from-c-code/

    public class AzureLogAnalytics
    {
        public AzureLogAnalytics(string workspaceId, string sharedKey, string logType, string apiVersion = "2016-04-01")
        {
            this.WorkspaceId = workspaceId;
            this.SharedKey = sharedKey;
            this.LogType = logType;
            this.ApiVersion = apiVersion;
        }

        public string WorkspaceId { get; }
        public string SharedKey { get; }
        public string ApiVersion { get; }
        public string LogType { get; }

        public void Post(byte[] content)
        {
            string requestUriString = $"https://{WorkspaceId}.ods.opinsights.azure.com/api/logs?api-version={ApiVersion}";
            DateTime dateTime = DateTime.UtcNow;
            string dateString = dateTime.ToString("r");
            string signature = GetSignature("POST", content.Length, "application/json", dateString, "/api/logs");
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

                Console.WriteLine(responseAsync.StatusDescription);

                // Get the stream containing content returned by the server.  
                // The using block ensures the stream is automatically closed.
                using (Stream dataStream = responseAsync.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(dataStream);
                    string responseFromServer = reader.ReadToEnd();
                    Console.WriteLine(responseFromServer);
                }
            }
        }

        private string GetSignature(string method, int contentLength, string contentType, string date, string resource)
        {
            string message = $"{method}\n{contentLength}\n{contentType}\nx-ms-date:{date}\n{resource}";
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            using (HMACSHA256 encryptor = new HMACSHA256(Convert.FromBase64String(SharedKey)))
            {
                return $"SharedKey {WorkspaceId}:{Convert.ToBase64String(encryptor.ComputeHash(bytes))}";
            }
        }
    }
}