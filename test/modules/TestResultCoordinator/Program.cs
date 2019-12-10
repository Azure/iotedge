// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TestResultCoordinator");

        static async Task Main(string[] args)
        {
            Logger.LogInformation($"Starting load gen with the following settings:\r\n{Settings.Current}");

            // TODO: Add Coordinator logic
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await TestOperationResultStorage.Instance.InitAsync(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.OptimizeForPerformance, Settings.Current.ResultSources);
            Console.WriteLine("TestOperationResultStorage fired up");
            await CreateWebHostBuilder(args).Build().RunAsync(cts.Token);
            Console.WriteLine("WebHostBuilder fired up");

            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Console.WriteLine("TestResultCoordinator Main() exited.");
        }

        static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls($"http://*:{Settings.Current.WebhostPort}")
                .UseStartup<Startup>();
    }
}
