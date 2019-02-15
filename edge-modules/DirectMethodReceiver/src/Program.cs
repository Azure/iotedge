// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodReceiver
{
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.module;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodReceiver");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Logger.LogInformation("DirectMethodReceiver Main() started.");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only);
            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                transportType,
                Logger,
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy).ConfigureAwait(false);

            await moduleClient.OpenAsync().ConfigureAwait(false);
            await moduleClient.SetMethodHandlerAsync("HelloWorldMethod", HelloWorldMethod, null).ConfigureAwait(false);

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            await WhenCancelled(cts.Token);
            return 0;
        }

        static Task<MethodResponse> HelloWorldMethod(MethodRequest methodRequest, object userContext)
        {
            Logger.LogInformation("Received direct method call.");
            return Task.FromResult(new MethodResponse((int)HttpStatusCode.OK));
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}
