// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
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
    using RestSharp;

    [EndToEnd]
    public class PlugAndPlay : SasManualProvisioningFixture
    {
        const string PlugAndPlayIdentityName = "PnPIdentity";
        const string TestModelId = "dtmi:edgeE2ETest:TestCapabilityModel;1";
        const string DeviceId = "pnpTestDeviceId";

        [Test]
        public async Task DeviceClient()
        {
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
                            ("deviceId", DeviceId),
                            ("iotHubConnectionString", Context.Current.ConnectionString)
                        });
            },
            token);
            EdgeModule filter = deployment.Modules[plugAndPlayIdentityImage];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
            this.Validate(this.iotHub.HubName, this.iotHub.Hostname, DeviceId, TestModelId);
        }


        public void Validate(string hubName, string hostName, string deviceId, string expectedModelId)
        {
            // Verify that the device has been registered as a plug and play device
            string sasToken = GenerateSasToken($"{this.iotHub.HubName}.{this.iotHub.Hostname}/devices/{DeviceId}", this.iotHub.SharedAccessKey, "iothubowner");
            var client = new RestClient($"https://{hubName}.{hostName}/digitaltwins/{deviceId}?api-version=2020-05-31-preview");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", sasToken);
            request.AddHeader("Content-Type", "application/json");
            IRestResponse response = client.Execute(request);
            var jo = JObject.Parse(response.Content);
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

            string token = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}", WebUtility.UrlEncode(resourceUri), WebUtility.UrlEncode(signature), expiry);

            if (!String.IsNullOrEmpty(policyName))
            {
                token += "&skn=" + policyName;
            }

            return token;
        }
    }
}
