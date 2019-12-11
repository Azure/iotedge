// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TestResultCoordinator");

        static async Task Main()
        {
            // TODO: Add Coordinator logic
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Console.WriteLine("TestResultCoordinator Main() exited.");
        }
        
        static async Task SetupEventReceiveHandlerAsync(string trackingId, string deviceId, string eventHubsConnectionString, string consumerGroupId, DateTime eventEnqueuedFrom)
        {
            var builder = new EventHubsConnectionStringBuilder(eventHubsConnectionString);
            Logger.LogInformation($"Receiving events from device '{deviceId}' on Event Hub '{builder.EntityPath}' enqueued at or after {eventEnqueuedFrom}");

            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(builder.ToString());

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                consumerGroupId,
                EventHubPartitionKeyResolver.ResolveToPartition(deviceId, (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnqueuedTime(eventEnqueuedFrom));

            eventHubReceiver.SetReceiveHandler(new PartitionReceiveHandler(trackingId, deviceId));
        }
    }
}
