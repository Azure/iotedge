// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodReceiver
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodReceiver");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            DirectMethodReceiver directMethodClient = null;
            try
            {
                Logger.LogInformation("DirectMethodReceiver Main() started.");

                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/appsettings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                directMethodClient = await DirectMethodReceiver.CreateAsync(
                    Logger,
                    configuration);

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                await directMethodClient.StartAsync();

                await cts.Token.WhenCanceled();

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
                Logger.LogInformation("DirectMethodReceiver Main() finished.");
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
            finally
            {
                directMethodClient?.Dispose();
            }
            return 0;
        }
    }
}
