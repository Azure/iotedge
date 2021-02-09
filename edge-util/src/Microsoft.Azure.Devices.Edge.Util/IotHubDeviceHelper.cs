// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common;
    using Newtonsoft.Json;

    public class IotHubDeviceHelper
    {
        const string ApiVersion = "api-version=2020-06-30-preview";
        const string RequestUriFormat = "/devices/{0}?{1}";
        static readonly TimeSpan TokenTimeToLive = TimeSpan.FromHours(1);
        ISignatureProvider signatureProvider;
        IotHubConnectionStringBuilder connectionStringBuilder;
        Uri iotHubBaseHttpsUri;
        HttpClient httpClient;

        public IotHubDeviceHelper(string connectionString)
        {
            this.connectionStringBuilder = IotHubConnectionStringBuilder.Create(connectionString);
            this.signatureProvider = new SharedAccessKeySignatureProvider(this.connectionStringBuilder.SharedAccessKey);
            this.iotHubBaseHttpsUri = new UriBuilder(Uri.UriSchemeHttps, this.connectionStringBuilder.HostName).Uri;

            this.httpClient = new HttpClient()
            {
                BaseAddress = this.iotHubBaseHttpsUri,
                Timeout = TimeSpan.FromSeconds(60),
            };
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            this.httpClient.DefaultRequestHeaders.ExpectContinue = false;
        }

        public async Task<IotHubDevice> GetDeviceAsync(string deviceId)
        {
            Uri uri = this.GetServiceUri(deviceId);

            using var msg = new HttpRequestMessage(HttpMethod.Get, uri);
            msg.Headers.Add(HttpRequestHeader.Authorization.ToString(), await this.GetToken());

            HttpResponseMessage responseMsg;
            responseMsg = await this.httpClient.SendAsync(msg);
            if (responseMsg == null)
            {
                throw new InvalidOperationException($"The response message was null when getting device: {deviceId}");
            }

            string resultString = await responseMsg.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IotHubDevice>(resultString);
        }

        public async Task<IotHubDevice> AddDeviceAsync(IotHubDevice device)
        {
            NormalizeDevice(device);

            Uri uri = this.GetServiceUri(device.Id);
            using var msg = new HttpRequestMessage(HttpMethod.Put, uri);
            msg.Headers.Add(HttpRequestHeader.Authorization.ToString(), await this.GetToken());

            string str = JsonConvert.SerializeObject(device);
            msg.Content = new StringContent(str, Encoding.UTF8, "application/json");

            HttpResponseMessage responseMsg;
            responseMsg = await this.httpClient.SendAsync(msg);
            if (responseMsg == null)
            {
                throw new InvalidOperationException($"The response message was null when adding a new device: {device.Id}");
            }

            string resultString = await responseMsg.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IotHubDevice>(resultString);
        }

        Uri GetServiceUri(string deviceId)
        {
            string encodedDeviceId = WebUtility.UrlEncode(deviceId);
            return new Uri(this.iotHubBaseHttpsUri, new Uri(RequestUriFormat.FormatInvariant(deviceId, ApiVersion), UriKind.Relative));
        }

        async Task<string> GetToken()
        {
            // Audience for service APIs is just the iothub hostname
            string audience = this.connectionStringBuilder.HostName;
            string expiresOn = SasTokenHelper.BuildExpiresOn(DateTime.UtcNow, TokenTimeToLive);
            string data = string.Join(
                "\n",
                new List<string>
                {
                audience,
                expiresOn
                });

            try
            {
                string signature = await this.signatureProvider.SignAsync(data);
                string token = SasTokenHelper.BuildSasToken(audience, signature, expiresOn);
                token += "&skn=" + this.connectionStringBuilder.SharedAccessKeyName;
                return token;
            }
            catch (SignatureProviderException e)
            {
                throw new TokenProviderException(e);
            }
        }

        static void NormalizeDevice(IotHubDevice device)
        {
            // auto generate keys if not specified
            if (device.Authentication == null)
            {
                device.Authentication = new AuthenticationMechanism();
            }

            NormalizeAuthenticationInfo(device.Authentication);
        }

        static void NormalizeAuthenticationInfo(AuthenticationMechanism authenticationInfo)
        {
            if (authenticationInfo.SymmetricKey != null && !authenticationInfo.SymmetricKey.IsEmpty())
            {
                authenticationInfo.Type = AuthenticationType.Sas;
            }

            if (authenticationInfo.X509Thumbprint != null && !authenticationInfo.X509Thumbprint.IsEmpty())
            {
                authenticationInfo.Type = AuthenticationType.SelfSigned;
            }
        }
    }
}
