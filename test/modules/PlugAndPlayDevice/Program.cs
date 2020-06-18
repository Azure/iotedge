// Copyright (c) Microsoft. All rights reserved.
namespace PlugAndPlayDevice
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Globalization;
    using System.Net;
    using System.Security.Cryptography;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("PlugAndPlayDeviceModule");

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // work left to be done here:
            // 1. Make test in Microsoft.Azure.Devices.Edge.Test -> Should be as simple as deploying the module and verifying that message was received AND pnp device was registered
            // 2. Either wait until service SDK has an API or use HTTP request with SAS token for verification purposes
            // Console.WriteLine($"SAS TOKEN: {generateSasToken("dybronso-pnp-hub.azure-devices.net/devices/pnpDevice5", "{PRIMARY_KEY_FROM_IOTHUBOWNER}", "iothubowner")}");
        }

        public static string generateSasToken(string resourceUri, string key, string policyName, int expiryInSeconds = 3600)
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

        public async Task StartAsync(CancellationToken ct)
        {
            // TODO: You cannot install certificate on Windows by script - we need to implement certificate verification callback handler.
            IEnumerable<X509Certificate2> certs = await CertificateHelper.GetTrustBundleFromEdgelet(new Uri(Settings.Current.WorkloadUri), Settings.Current.ApiVersion, Settings.Current.ApiVersion, Settings.Current.ModuleId, Settings.Current.ModuleGenerationId);
            ITransportSettings transportSettings = ((Protocol)Enum.Parse(typeof(Protocol), Settings.Current.TransportType.ToString())).ToTransportSettings();
            OsPlatform.Current.InstallCaCertificates(certs, transportSettings);
            Microsoft.Azure.Devices.RegistryManager registryManager = null;
            DeviceClient deviceClient = null;
            try
            {
                registryManager = Microsoft.Azure.Devices.RegistryManager.CreateFromConnectionString(Settings.Current.IotHubConnectionString);
                Microsoft.Azure.Devices.Device device = await registryManager.AddDeviceAsync(new Microsoft.Azure.Devices.Device(Settings.Current.DeviceId), ct);
                string deviceConnectionString = $"HostName={Settings.Current.IotHubHostName};DeviceId={Settings.Current.DeviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={Settings.Current.GatewayHostName}";
                ClientOptions clientOptions = new ClientOptions { ModelId = "dtmi:edgeE2ETest:EnvironmentalSensor;1" };

                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, new ITransportSettings[] { transportSettings }, clientOptions);
                
            }
            catch (Exception ex)
            {
                Logger.LogError("Plug and play device creation failed", ex);
                throw;
            }

            try
            {
                await deviceClient.SendEventAsync(new Message(Encoding.ASCII.GetBytes("test message")));
            }
            catch(Exception ex)
            {
                Logger.LogError("Plug and play device sending failed", ex);
                throw;
            }

            // Verify that the device has been registered as a plug and play device
            // Put verification logic in test module 
            HttpClient httpClient = new HttpClient();
            string requestUriString = $"https://{Settings.Current.IotHubHostName}.{{hostName}}/digitaltwins/{{deviceId}}?api-version=2020-05-31-preview";
        }
    }
}
