// Copyright (c) Microsoft. All rights reserved.
namespace DebugNetworkStatusChange
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics.Tracing;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DebugNetworkStatusChange");
        private readonly ConsoleEventListener _listener = new ConsoleEventListener("Microsoft-Azure-Devices-Device-Client");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            // (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromHours(3), Logger);

            // IConfiguration configuration = new ConfigurationBuilder()
            //     .SetBasePath(Directory.GetCurrentDirectory())
            //     .AddJsonFile("config/appsettings.json", optional: true)
            //     .AddEnvironmentVariables()
            //     .Build();

            try
            {
                Environment.SetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME", string.Empty);
                // ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                //     TransportType.Amqp_Tcp_Only,
                //     ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                //     ModuleUtil.DefaultTransientRetryStrategy,
                //     Logger);

                var retryPolicy = new RetryPolicy(ModuleUtil.DefaultTimeoutErrorDetectionStrategy, ModuleUtil.DefaultTransientRetryStrategy);
                retryPolicy.Retrying += (_, args) =>
                {
                    Console.WriteLine($"Retry {args.CurrentRetryCount} times to create module client and failed with exception:{Environment.NewLine}{args.LastException}");
                };

                ModuleClient client = await retryPolicy.ExecuteAsync(() => InitializeModuleClientAsync(TransportType.Amqp_Tcp_Only, Logger));

                async Task<ModuleClient> InitializeModuleClientAsync(TransportType transportType, ILogger logger)
                {
                    var amqpSettings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
                    amqpSettings.IdleTimeout = TimeSpan.FromSeconds(60);
                    ITransportSettings[] settings = new ITransportSettings[] {amqpSettings};

                    NonStaticClass nsc = new NonStaticClass(Logger);

                    ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                    moduleClient.SetConnectionStatusChangesHandler(nsc.StatusChangedHandler);
                    await moduleClient.OpenAsync();

                    return moduleClient;
                }

                while (client!=null)
                {
                    // Console.WriteLine($"{DateTime.UtcNow} Waiting for status change");
                    await Task.Delay(TimeSpan.FromMilliseconds(5));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.UtcNow} Exception {ex}");
            }

            // completed.Set();
            // handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("DebugNetworkStatusChange Main() finished.");

            return 0;
        }
    }
}
