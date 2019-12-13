// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TestResultCoordinator");

        static async Task Main(string[] args)
        {
            Logger.LogInformation($"Starting TestResultCoordinator with the following settings:\r\n{Settings.Current}");

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await SetupEventReceiveHandlerAsync(Settings.Current, DateTime.UtcNow);

            // Continue after delay to report test results
            Task.Delay(Settings.Current.TestDuration + Settings.Current.DurationBeforeVerification, cts.Token)
                .ContinueWith(async _ => await ReportTestResultsAsync(), cts.Token);

            await TestOperationResultStorage.InitAsync(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.OptimizeForPerformance, Settings.Current.ResultSources);
            Logger.LogInformation("TestOperationResultStorage created successfully");
            Logger.LogInformation("Creating WebHostBuilder...");
            await CreateWebHostBuilder(args).Build().RunAsync(cts.Token);

            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Console.WriteLine("TestResultCoordinator Main() exited.");
        }

        static Task ReportTestResultsAsync()
        {
            // TODO: Generate report and report to Log Analytic
            return Task.CompletedTask;
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
