// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Identity;
    using Azure.Messaging.EventHubs.Consumer;
    using Azure.Messaging.EventHubs.Primitives;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Storage;

    class TestResultEventReceivingService : BackgroundService
    {
        readonly ILogger logger = ModuleUtil.CreateLogger(nameof(TestResultEventReceivingService));
        readonly ITestOperationResultStorage storage;
        readonly TestResultEventReceivingServiceSettings serviceSpecificSettings;

        public TestResultEventReceivingService(ITestOperationResultStorage storage)
        {
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
            this.serviceSpecificSettings = Settings.Current.TestResultEventReceivingServiceSettings.Expect(() => new ArgumentException("TestResultEventReceivingServiceSettings must be supplied."));
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Test Result Event Receiving Service is stopping.");

            await Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            this.logger.LogInformation("Test Result Event Receiving Service running.");

            DateTime eventEnqueuedFrom = DateTime.UtcNow;

            var consumer = new EventHubConsumerClient(
                this.serviceSpecificSettings.ConsumerGroupName,
                this.serviceSpecificSettings.EventHubNamespace,
                this.serviceSpecificSettings.EventHubName,
                new WorkloadIdentityCredential());
            int numPartitions = (await consumer.GetPartitionIdsAsync()).Length;
            await consumer.CloseAsync();

            var handler = new PartitionReceiveHandler(Settings.Current.TrackingId, Settings.Current.DeviceId, this.storage);

            while (!cancellationToken.IsCancellationRequested)
            {
                var receiver = new PartitionReceiver(
                    this.serviceSpecificSettings.ConsumerGroupName,
                    EventHubPartitionKeyResolver.ResolveToPartition(Settings.Current.DeviceId, numPartitions),
                    EventPosition.FromEnqueuedTime(eventEnqueuedFrom),
                    this.serviceSpecificSettings.EventHubNamespace,
                    this.serviceSpecificSettings.EventHubName,
                    new WorkloadIdentityCredential());

                this.logger.LogDebug($"Receiving events from device '{Settings.Current.DeviceId}' on Event Hub '{this.serviceSpecificSettings.EventHubName}' enqueued on or after {eventEnqueuedFrom}");

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var batch = await receiver.ReceiveBatchAsync(50, cancellationToken);
                        await handler.ProcessEventsAsync(batch);
                    }
                }
                catch (Azure.Messaging.EventHubs.EventHubsException e) when (e.IsTransient)
                {
                    this.logger.LogWarning(e, "Transient Event Hubs error; recreating receiver.");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // This is expected when the service is stopping.
                    break;
                }
                finally
                {
                    await receiver.CloseAsync();
                }
            }

            this.logger.LogInformation($"Finish ExecuteAsync method in {nameof(TestResultEventReceivingService)}");
        }
    }
}
