// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TestAnalyzer));

        static async Task Main(string[] args)
        {
            Logger.LogInformation($"Starting analyzer with the following settings:\r\n{Settings.Current}");

            DateTime lastReceivedMessageTime = await LoadStartupTimeFromStorageAsync();
            await ReceiveMessagesAsync(lastReceivedMessageTime);

            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            var tcs = new TaskCompletionSource<bool>();
            cts.Token.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);

            await CreateWebHostBuilder(args).Build().RunAsync(cts.Token);
        }

        static async Task<DateTime> LoadStartupTimeFromStorageAsync()
        {
            DateTime lastReceivedAt = DateTime.MinValue;
            await ReportingCache.Instance.InitAsync(Settings.Current.StoragePath, Settings.Current.OptimizeForPerformance);
            IDictionary<string, IList<SortedSet<MessageDetails>>> messagesSnapShot = ReportingCache.Instance.GetMessagesSnapshot();
            foreach (KeyValuePair<string, IList<SortedSet<MessageDetails>>> moduleMesssages in messagesSnapShot)
            {
                foreach (SortedSet<MessageDetails> moduleMesssage in moduleMesssages.Value)
                {
                    if (lastReceivedAt < moduleMesssage.Last().EnqueuedDateTime)
                    {
                        lastReceivedAt = moduleMesssage.Last().EnqueuedDateTime;
                    }
                }
            }

            return lastReceivedAt;
        }

        static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls($"http://*:{Settings.Current.WebhostPort}")
                .UseStartup<Startup>();

        static async Task ReceiveMessagesAsync(DateTime lastReceivedMesssage)
        {
            var builder = new EventHubsConnectionStringBuilder(Settings.Current.EventHubConnectionString);
            Logger.LogInformation($"Receiving events from device '{Settings.Current.DeviceId}' on Event Hub '{builder.EntityPath}' with last received message at {lastReceivedMesssage.ToString()}");

            EventHubClient eventHubClient =
                EventHubClient.CreateFromConnectionString(builder.ToString());

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                Settings.Current.ConsumerGroupId,
                EventHubPartitionKeyResolver.ResolveToPartition(Settings.Current.DeviceId, (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnqueuedTime(lastReceivedMesssage == DateTime.MinValue ? DateTime.UtcNow : lastReceivedMesssage));

            eventHubReceiver.SetReceiveHandler(new PartitionReceiveHandler(Settings.Current.DeviceId, Settings.Current.ExcludedModuleIds));
        }
    }
}
