// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TestResultCoordinator");

        static async Task Main(string[] args)
        {
            Logger.LogInformation($"Starting TestResultCoordinator with the following settings:\r\n{Settings.Current}");

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await SetupEventReceiveHandlerAsync(Settings.Current, DateTime.UtcNow);

            await TestOperationResultStorage.InitAsync(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.OptimizeForPerformance, Settings.Current.ResultSources);
            Logger.LogInformation("TestOperationResultStorage created successfully");

            Logger.LogInformation("Creating WebHostBuilder...");
            Task webHost = CreateWebHostBuilder(args).Build().RunAsync(cts.Token);

            await Task.WhenAny(cts.Token.WhenCanceled(), webHost);

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("TestResultCoordinator Main() exited.");
        }

        static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls($"http://*:{Settings.Current.WebHostPort}")
                .UseStartup<Startup>();

        static async Task SetupEventReceiveHandlerAsync(Settings settings, DateTime eventEnqueuedFrom)
        {
            var builder = new EventHubsConnectionStringBuilder(settings.EventHubConnectionString);
            Logger.LogInformation($"Receiving events from device '{settings.DeviceId}' on Event Hub '{builder.EntityPath}' enqueued at or after {eventEnqueuedFrom}");

            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(builder.ToString());

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                settings.ConsumerGroupName,
                EventHubPartitionKeyResolver.ResolveToPartition(settings.DeviceId, (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnqueuedTime(eventEnqueuedFrom));

            eventHubReceiver.SetReceiveHandler(new PartitionReceiveHandler(settings.TrackingId, settings.DeviceId));
        }
    }
}
