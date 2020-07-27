// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    public class PlugAndPlay : SasManualProvisioningFixturePreview
    {
        const string PlugAndPlayIdentityName = "PnPIdentity";
        const string TestModelId = "dtmi:edgeE2ETest:TestCapabilityModel;1";
        const string DeviceIdPrefix = "pnpDevice-";

        [Test]
        public async Task DeviceClient()
        {
            string deviceId = DeviceIdPrefix + Guid.NewGuid();
            string plugAndPlayIdentityImage = Context.Current.PlugAndPlayIdentityImage.Expect(() => new InvalidOperationException("Missing Plug and Play Identity image"));
            CancellationToken token = this.TestToken;

            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
            builder =>
            {
                builder.GetModule(ModuleName.EdgeHub).WithEnvironment(new[] { ("UpstreamProtocol", "Mqtt") });
                builder.AddModule(PlugAndPlayIdentityName, plugAndPlayIdentityImage)
                    .WithEnvironment(
                        new[]
                        {
                            ("modelId", TestModelId),
                            ("deviceId", deviceId),
                            ("iotHubConnectionString", Context.Current.PreviewConnectionString)
                        });
            },
            token);
            EdgeModule filter = deployment.Modules[PlugAndPlayIdentityName];
            await Task.Delay(TimeSpan.FromSeconds(15));
            // await filter.WaitForEventsReceivedFromDeviceAsync(deployment.StartTime, token, DeviceId);
            await this.Validate(this.iotHub.Hostname, deviceId, TestModelId);
        }

        public async Task Validate(string hostName, string deviceId, string expectedModelId)
        {
            // Verify that the device has been registered as a plug and play device
            string sasToken = GenerateSasToken($"{this.iotHub.Hostname}/devices/{deviceId}", this.iotHub.SharedAccessKey, "iothubowner");
            Log.Verbose($"AccessKey: {this.iotHub.SharedAccessKey}");
            Log.Verbose($"SAS: {sasToken}");

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(sasToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            Log.Verbose($"Request string: https://{hostName}/digitaltwins/{deviceId}?api-version=2020-05-31-preview");
            HttpResponseMessage responseMessage = await httpClient.GetAsync($"https://{hostName}/digitaltwins/{deviceId}?api-version=2020-05-31-preview");
            Log.Verbose($"HTTPCLIENT method response status code: {responseMessage.StatusCode}");
            Log.Verbose($"HTTPCLIENT method response headers: {responseMessage.Headers}");
            Log.Verbose($"HTTPCLIENT method response content: {await responseMessage.Content.ReadAsStringAsync()}");
            var jo = JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
            var modelId = jo["$metadata"]["$model"].ToString();
            Assert.AreEqual(expectedModelId, modelId);
        }

        public static string GenerateSasToken(string resourceUri, string key, string policyName, int expiryInSeconds = 3600)
        {
            TimeSpan fromEpochStart = DateTime.UtcNow - new DateTime(1970, 1, 1);
            string expiry = Convert.ToString((int)fromEpochStart.TotalSeconds + expiryInSeconds);

            string stringToSign = WebUtility.UrlEncode(resourceUri) + "\n" + expiry;

            HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(key));
            string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

            string token = string.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}", WebUtility.UrlEncode(resourceUri), WebUtility.UrlEncode(signature), expiry);

            if (!string.IsNullOrEmpty(policyName))
            {
                token += "&skn=" + policyName;
            }

            return token;
        }
    }
}
