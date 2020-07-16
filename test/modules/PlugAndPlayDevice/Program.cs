// Copyright (c) Microsoft. All rights reserved.
namespace PlugAndPlayDevice
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("PlugAndPlayDeviceModule");

        static async Task Main(string[] args)
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await StartAsync(cts.Token);

            Logger.LogInformation("Finish sending messages.");
            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
        }

        public static async Task StartAsync(CancellationToken ct)
        {
            // TODO: You cannot install certificate on Windows by script - we need to implement certificate verification callback handler.
            IEnumerable<X509Certificate2> certs = await CertificateHelper.GetTrustBundleFromEdgelet(new Uri(Settings.Current.WorkloadUri), Settings.Current.ApiVersion, Settings.Current.ApiVersion, Settings.Current.ModuleId, Settings.Current.ModuleGenerationId);
            ITransportSettings transportSettings = ((Protocol)Enum.Parse(typeof(Protocol), Settings.Current.TransportType.ToString())).ToTransportSettings();
            OsPlatform.Current.InstallCaCertificates(certs, transportSettings);
            DeviceClient deviceClient;
            try
            {
                Microsoft.Azure.Devices.RegistryManager registryManager = Microsoft.Azure.Devices.RegistryManager.CreateFromConnectionString(Settings.Current.IotHubConnectionString);
                Microsoft.Azure.Devices.Device device = await registryManager.AddDeviceAsync(new Microsoft.Azure.Devices.Device(Settings.Current.DeviceId), ct);
                string deviceConnectionString = $"HostName={Settings.Current.IotHubHostName};DeviceId={Settings.Current.DeviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={Settings.Current.GatewayHostName}";
                ClientOptions clientOptions = new ClientOptions { ModelId = Settings.Current.ModelId };

                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, new ITransportSettings[] { transportSettings }, clientOptions);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plug and play device creation failed", ex);
                throw;
            }

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await deviceClient.SendEventAsync(new Message(Encoding.ASCII.GetBytes("test message")));
                    Logger.LogInformation("Successfully sent message");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Plug and play device sending failed", ex);
                throw;
            }
        }
    }
}
