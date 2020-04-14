// Copyright (c) Microsoft. All rights reserved.
namespace DemoApp
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics.Tracing;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    class Program
    {
        private readonly ConsoleEventListener _listener = new ConsoleEventListener("Microsoft-Azure-Devices-Device-Client");
        private const string ModuleConnectionString = "";

        static async Task Main(string[] args)
        {

            try
            {
                Environment.SetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME", string.Empty);
                Environment.SetEnvironmentVariable("IOTEDGE_MODULEGENERATIONID", "");
                Environment.SetEnvironmentVariable("IOTEDGE_APIVERSION", "2019-11-05");
                Environment.SetEnvironmentVariable("IOTEDGE_IOTHUBHOSTNAME", "bearIoTHub.azure-devices.net");
                Environment.SetEnvironmentVariable("RuntimeLogLevel", "Debug");
                Environment.SetEnvironmentVariable("IOTEDGE_MODULEID", "dnsc");
                Environment.SetEnvironmentVariable("IOTEDGE_AUTHSCHEME", "sasToken");
                Environment.SetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME", "iotedge-seabear");
                Environment.SetEnvironmentVariable("IOTEDGE_DEVICEID", "iotedge-seabear");
                Environment.SetEnvironmentVariable("UpstreamProtocol", "Amqp");
                Environment.SetEnvironmentVariable("IOTEDGE_WORKLOADURI", "unix:///var/run/iotedge/workload.sock");
                Environment.SetEnvironmentVariable("PATH", "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin");
                Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://+:80");
                Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");
                Environment.SetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "true");
                Environment.SetEnvironmentVariable("DOTNET_VERSION", "2.1.13");
                Environment.SetEnvironmentVariable("MODULE_NAME", "DebugNetworkStatusChange.dll");

                ModuleClient moduleClient = null;

                var retryPolicy = new RetryPolicy(ModuleUtil.DefaultTimeoutErrorDetectionStrategy, ModuleUtil.DefaultTransientRetryStrategy);
                retryPolicy.Retrying += (_, args1) =>
                {
                    Console.WriteLine($"Retry {args1.CurrentRetryCount} times to create module client and failed with exception:{Environment.NewLine}{args1.LastException}");
                };

                ModuleClient client = await retryPolicy.ExecuteAsync(() => InitializeModuleClientAsync(TransportType.Amqp_Tcp_Only));

                async Task<ModuleClient> InitializeModuleClientAsync(TransportType transportType)
                {
                    var amqpSettings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
                    amqpSettings.IdleTimeout = TimeSpan.FromSeconds(60);
                    ITransportSettings[] settings = new ITransportSettings[] { amqpSettings };

                    NonStaticClass nsc = new NonStaticClass();

                    // ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                    moduleClient = ModuleClient.CreateFromConnectionString(ModuleConnectionString, settings);
                    moduleClient.SetConnectionStatusChangesHandler(nsc.StatusChangedHandler);
                    await moduleClient.OpenAsync();

                    return moduleClient;
                }

                while (client != null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                }

                moduleClient?.CloseAsync()?.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.UtcNow} Exception {ex}");
            }

            Console.WriteLine("DebugNetworkStatusChange Main() finished.");
        }
    }
}
