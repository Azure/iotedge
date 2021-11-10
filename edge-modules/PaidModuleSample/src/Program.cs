// Copyright (c) Microsoft. All rights reserved.
namespace PaidModuleSample
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Console.WriteLine("PaidModuleSample Main() started.");

            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

            IConfiguration configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("config/appsettings.json", optional: true)
               .AddEnvironmentVariables()
               .Build();

            string iotHubHostName = configuration.GetValue<string>("IOTEDGE_IOTHUBHOSTNAME");
            string deviceId = configuration.GetValue<string>("IOTEDGE_DEVICEID");
            string moduleId = configuration.GetValue<string>("IOTEDGE_MODULEID");
            string generationId = configuration.GetValue<string>("IOTEDGE_MODULEGENERATIONID");
            string providerUri = configuration.GetValue<string>("IOTEDGE_WORKLOADURI");
            string version = configuration.GetValue<string>("IOTEDGE_APIVERSION"); // "2020-10-10"
            string gateway = configuration.GetValue<string>("IOTEDGE_GATEWAYHOSTNAME");

            string url = $"https://{gateway}/devices/{deviceId}/modules/{moduleId}/purchase";
            var signatureProvider = new SignatureProvider(moduleId, generationId, providerUri);

            // Install trust bundle certificates
            await InstallCertificates(signatureProvider);

            Console.WriteLine("Getting token");
            var token = await GetTokenAsync(iotHubHostName, deviceId, moduleId, DateTime.Now, TimeSpan.FromDays(1), signatureProvider);
            Console.WriteLine("Token retrieved");

            Console.WriteLine($"Send request to {url}");
            var response = await ConstructRequestAndFetchResponseAsync(url, token, HttpMethod.Get);
            var purchase = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response {purchase}");

            await WhenCanceled(cts.Token);

            Console.WriteLine("PaidModuleSample Main() finished.");
            return 0;
        }

        public static Task WhenCanceled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        static async Task<HttpResponseMessage> ConstructRequestAndFetchResponseAsync(string url, string token, HttpMethod httpMethod)
        {
            var request = new HttpRequestMessage(httpMethod, url)
            {
                Content = ConstructRequestContent(string.Empty)
            };

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);

            HttpResponseMessage response = await httpClient.SendAsync(request);

            return response;
        }

        static ByteArrayContent ConstructRequestContent(string content)
        {
            var byteObject = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
            byteObject.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return byteObject;
        }

       
        static async Task<string> GetTokenAsync(string iotHubHostName, string deviceId, string moduleId, DateTime startTime, TimeSpan ttl, SignatureProvider signatureProvider)
        {
            string audience = SasTokenHelper.BuildAudience(iotHubHostName, deviceId, moduleId);
               
            string expiresOn = SasTokenHelper.BuildExpiresOn(startTime, ttl);
            string data = string.Join(
                "\n",
                new List<string>
                {
                    audience,
                    expiresOn
                });
           
            string signature = await signatureProvider.SignAsync(data);
            return SasTokenHelper.BuildSasToken(audience, signature, expiresOn);
        }

        public static IEnumerable<X509Certificate2> GetCertificatesFromPem(IEnumerable<string> rawPemCerts) =>
            rawPemCerts
                .Select(c => Encoding.UTF8.GetBytes(c))
                .Select(c => new X509Certificate2(c))
                .ToList();

        public static IList<string> ParsePemCerts(string pemCerts)
        {
            if (string.IsNullOrEmpty(pemCerts))
            {
                throw new InvalidOperationException("Trusted certificates can not be null or empty.");
            }

            // Extract each certificate's string. The final string from the split will either be empty
            // or a non-certificate entry, so it is dropped.
            string delimiter = "-----END CERTIFICATE-----";
            string[] rawCerts = pemCerts.Split(new[] { delimiter }, StringSplitOptions.None);
            return rawCerts
                .Take(rawCerts.Count() - 1) // Drop the invalid entry
                .Select(c => $"{c}{delimiter}")
                .ToList(); // Re-add the certificate end-marker which was removed by split
        }

        public static async Task InstallCertificates(SignatureProvider signatureProvider)
        {
            Console.WriteLine("Getting trustbundle");
            var trustBundle = await signatureProvider.GetTrustBundleAsync();
            IEnumerable<X509Certificate2> certificateChain = GetCertificatesFromPem(ParsePemCerts(trustBundle));
            X509Certificate2[] certs = certificateChain.ToArray();

            StoreName storeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root;

            using (var store = new X509Store(storeName, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                foreach (X509Certificate2 cert in certs)
                {
                    store.Add(cert);
                }
            }

            Console.WriteLine($"Installed trustbundle {certs.Count()} certificates to {storeName}");
        }

        public class SasTokenHelper
        {
            const string SharedAccessSignature = "SharedAccessSignature";
            const string AudienceFieldName = "sr";
            const string SignatureFieldName = "sig";
            const string ExpiryFieldName = "se";
            static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            public static string BuildSasToken(string audience, string signature, string expiry)
            {
                // Example returned string:
                // SharedAccessSignature sr=ENCODED(dh://myiothub.azure-devices.net/a/b/c?myvalue1=a)&sig=<Signature>&se=<ExpiresOnValue>[&skn=<KeyName>]
                var buffer = new StringBuilder();
                buffer.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0} {1}={2}&{3}={4}&{5}={6}",
                    SharedAccessSignature,
                    AudienceFieldName,
                    audience,
                    SignatureFieldName,
                    WebUtility.UrlEncode(signature),
                    ExpiryFieldName,
                    WebUtility.UrlEncode(expiry));

                return buffer.ToString();
            }

            public static string BuildExpiresOn(DateTime startTime, TimeSpan timeToLive)
            {
                DateTime expiresOn = startTime.Add(timeToLive);
                TimeSpan secondsFromBaseTime = expiresOn.Subtract(EpochTime);
                long seconds = Convert.ToInt64(secondsFromBaseTime.TotalSeconds, CultureInfo.InvariantCulture);
                return Convert.ToString(seconds, CultureInfo.InvariantCulture);
            }

            /// <summary>
            /// Builds the audience from iothub deviceId and moduleId.
            /// Note that deviceId and moduleId need to be double encoded.
            /// </summary>
            public static string BuildAudience(string iotHub, string deviceId, string moduleId) =>
                WebUtility.UrlEncode($"{iotHub}/devices/{WebUtility.UrlEncode(deviceId)}/modules/{WebUtility.UrlEncode(moduleId)}");

            /// <summary>
            /// Builds the audience from iothub and deviceId.
            /// Note that deviceId and moduleId need to be double encoded.
            /// </summary>
            public static string BuildAudience(string iotHub, string deviceId) =>
                WebUtility.UrlEncode($"{iotHub}/devices/{WebUtility.UrlEncode(deviceId)}");
        }
    }
}
