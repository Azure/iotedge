// Copyright (c) Microsoft. All rights reserved.
namespace DebugNetworkStatusChange
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DebugNetworkStatusChange");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only),
            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                TransportType.Amqp_Tcp_Only,
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy,
                Logger);

            NonStaticClass nsc = new NonStaticClass(Logger);

            moduleClient.SetConnectionStatusChangesHandler(nsc.StatusChangedHandler);
            await moduleClient.OpenAsync();

            await Task.Delay(new TimeSpan(2, 5, 00));

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("DebugNetworkStatusChange Main() finished.");

            return 0;
        }
    }
}
