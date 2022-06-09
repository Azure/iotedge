// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.EventHubs;
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
            var builder = new EventHubsConnectionStringBuilder(this.serviceSpecificSettings.EventHubConnectionString);
            this.logger.LogDebug($"Receiving events from device '{Settings.Current.DeviceId}' on Event Hub '{builder.EntityPath}' enqueued on or after {eventEnqueuedFrom}");

            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(builder.ToString());
            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                this.serviceSpecificSettings.ConsumerGroupName,
                EventHubPartitionKeyResolver.ResolveToPartition(Settings.Current.DeviceId, (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnqueuedTime(eventEnqueuedFrom));
            eventHubReceiver.SetReceiveHandler(new PartitionReceiveHandler(Settings.Current.TrackingId, Settings.Current.DeviceId, this.storage));

            await cancellationToken.WhenCanceled();

            this.logger.LogInformation($"Finish ExecuteAsync method in {nameof(TestResultEventReceivingService)}");
        }
    }
}
