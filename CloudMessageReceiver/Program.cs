// Copyright (c) Microsoft. All rights reserved.
namespace CloudMessageReceiver
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
        static readonly ILogger Logger = ModuleUtil.CreateLogger("CloudMessageReceiver");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Receiver receiver = null;

            try
            {
                Logger.LogInformation("CloudMessageReceiver Main() started.");

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/appsettings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                receiver = new Receiver(Logger, configuration);
                await receiver.InitAsync(cts);

                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
                Logger.LogInformation("CloudMessageReceiver Main() finished.");
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
            finally
            {
                receiver?.Dispose();
            }

            return 0;
        }
    }
}
