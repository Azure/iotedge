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
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    public class PlugAndPlay : SasManualProvisioningFixture
    {
        const string TestModelId = "dtmi:edgeE2ETest:TestCapabilityModel;1";
        const string LoadGenModuleName = "loadGenModule";

        public PlugAndPlay()
            : base(
            Context.Current.PreviewConnectionString.Expect<ArgumentException>(() => throw new ArgumentException("Must supply preview connection string for PlugAndPlay tests.")),
            Context.Current.PreviewEventHubEndpoint.Expect<ArgumentException>(() => throw new ArgumentException("Must supply preview Event Hub endpoint for PlugAndPlay tests.")))
        {
        }

        [Test]
        public async Task DeviceClient()
        {
            CancellationToken token = this.TestToken;
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.GetModule(ModuleName.EdgeHub).WithEnvironment(new[] { ("UpstreamProtocol", "Mqtt") });
                },
                token);

            string leafDeviceId = DeviceId.Current.Generate();

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Mqtt,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                CertificateAuthority.GetQuickstart(),
                this.iotHub,
                token,
                Option.Some(TestModelId));

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    await leaf.WaitForEventsReceivedAsync(seekTime, token);
                    await this.ValidateDevice(leafDeviceId, TestModelId);
                },
                async () =>
                {
                    await leaf.DeleteIdentityAsync(token);
                });
        }

        [Test]
        public async Task ModuleClient()
        {
            CancellationToken token = this.TestToken;
            string loadGenImage = Context.Current.LoadGenImage.Expect(() => new ArgumentException("loadGenImage parameter is required for Priority Queues test"));
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(
                builder =>
                {
                    builder.GetModule(ModuleName.EdgeHub).WithEnvironment(new[] { ("UpstreamProtocol", "Mqtt") });
                    builder.AddModule(LoadGenModuleName, loadGenImage)
                    .WithEnvironment(new[]
                    {
                            ("testStartDelay", "00:00:00"),
                            ("messageFrequency", "00:00:00.5"),
                            ("transportType", Client.TransportType.Mqtt.ToString()),
                            ("modelId", TestModelId)
                    });
                },
                token);
            EdgeModule filter = deployment.Modules[LoadGenModuleName];
            await filter.WaitForEventsReceivedAsync(deployment.StartTime, token);
            await this.ValidateModule(this.runtime.DeviceId, LoadGenModuleName, TestModelId);
        }

        async Task ValidateModule(string deviceId, string moduleId, string expectedModelId)
        {
            string requestString = $"https://{this.iotHub.Hostname}/twins/{deviceId}/modules/{moduleId}?api-version=2020-05-31-preview";
            var jo = await this.MakeHttpGetRequest(requestString, deviceId);
            var modelId = jo["modelId"].ToString();
            Assert.AreEqual(expectedModelId, modelId);
        }

        async Task ValidateDevice(string deviceId, string expectedModelId)
        {
            // Verify that the device has been registered as a plug and play device
            string requestString = $"https://{this.iotHub.Hostname}/digitaltwins/{deviceId}/?api-version=2020-05-31-preview";
            var jo = await this.MakeHttpGetRequest(requestString, deviceId);
            var modelId = jo["$metadata"]["$model"].ToString();
            Assert.AreEqual(expectedModelId, modelId);
        }

        async Task<JObject> MakeHttpGetRequest(string requestString, string deviceId)
        {
            HttpClient httpClient = this.SetupHttpClient(deviceId);
            Log.Verbose($"Request string: {requestString}");
            HttpResponseMessage responseMessage = await httpClient.GetAsync(requestString);
            Log.Verbose($"HttpClient method response status code: {responseMessage.StatusCode}");
            Log.Verbose($"Got this from response: {await responseMessage.Content.ReadAsStringAsync()}");
            return JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        }

        HttpClient SetupHttpClient(string deviceId)
        {
            // We must generate a SAS token and use the endpoint until the service SDK comes out with a way to get the
            // modelId from the device's digital twin.
            string sasToken = GenerateSasToken($"{this.iotHub.Hostname}/devices/{deviceId}", this.iotHub.SharedAccessKey, "iothubowner");
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(sasToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
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
