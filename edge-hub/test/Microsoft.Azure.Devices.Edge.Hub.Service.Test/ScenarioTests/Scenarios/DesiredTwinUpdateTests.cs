// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    [Scenario]
    public class DesiredTwinUpdateTests
    {
        private const int SmallPack = 100;

        [RunnableInDebugOnly]
        public async Task TwinUpdateGetsReceived()
        {
            var cloudClientOpened = new TestMilestone();

            var deliverable = TwinCollectionDeliverable
                                .Create()
                                .WithPackSize(SmallPack)
                                .WithTimingStrategy<LinearTimingStrategy>(
                                    timing => timing.WithDelay(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(7)));

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<AllGoodClientBuilder>(
                                                             clientProvider => clientProvider
                                                                                    .WithOpenAction(cloudClientOpened.Passed)))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithDeviceProxy<AllGoodDeviceProxy>(
                                 proxy => proxy.WithDesiredPropertyUpdateAction(deliverable.ConfirmDelivery))
                             .WithEdgeHub(edgeHub)
                             .Build();

            await cloudClientOpened.WaitAsync();

            var cloudClients = await GenericClientProvider.GetProvidedInstancesAsync(device.Identity.Id, Utils.CancelAfter(TimeSpan.FromSeconds(60)));
            var cloudClient = cloudClients.First() as AllGoodClient;

            await Utils.WaitForAsync(() => cloudClient.HasUpdateDesiredPropertySubscription, Utils.CancelAfter(TimeSpan.FromSeconds(60)));

            await deliverable.StartDeliveringAsync(cloudClient.UpdateDesiredProperty);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));
        }

        [RunnableInDebugOnly]
        public async Task TwinUpdateGetsReceivedAfterCloudReconnectFromDisposedError()
        {
            var cloudClientOpened = new TestMilestone();

            var twinDeliverable = TwinCollectionDeliverable
                                    .Create()
                                    .WithPackSize(SmallPack)
                                    .WithTimingStrategy<LinearTimingStrategy>(
                                        timing => timing.WithDelay(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(7)));

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                 .WithThrowTypeStrategy<SingleThrowTypeStrategy>(
                                                                        throwTypeStrategy => throwTypeStrategy.WithType<ObjectDisposedException>())
                                                                 .WithThrowTimingStrategy<RandomThrowTimingStrategy>(
                                                                        throwTimingStrategy => throwTimingStrategy.WithOddsToThrow(0.8)) // high odds to throw, but sometimes let messages out
                                                                 .WithOpenAction(cloudClientOpened.Passed)))
                                                         .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithDeviceProxy<AllGoodDeviceProxy>(
                                 proxy => proxy.WithDesiredPropertyUpdateAction(twinDeliverable.ConfirmDelivery))
                             .WithEdgeHub(edgeHub)
                             .Build();

            await cloudClientOpened.WaitAsync();

            var cloudClients = await GenericClientProvider.GetProvidedInstancesAsync(device.Identity.Id, Utils.CancelAfter(TimeSpan.FromSeconds(60)));
            var cloudClient = cloudClients.First() as FlakyClient;

            await Utils.WaitForAsync(() => cloudClient.HasUpdateDesiredPropertySubscription, Utils.CancelAfter(TimeSpan.FromSeconds(60)));

            Task TwinDeliveryAction(Shared.TwinCollection twin) =>
                    GenericClientProvider.ExecuteOnLastOneAsync(
                            device.Identity.Id,
                            client => (client as FlakyClient).UpdateDesiredProperty(twin),
                            Utils.CancelAfter(TimeSpan.FromSeconds(60)));

            var allTwinUpdatesSent = new TestMilestone();

            var sendingMessagesTask = DeviceMessageDeliverable
                                        .Create()
                                        .WithVolumeStrategy<ConditionBasedVolumeStrategy>(
                                                volumeStrategy => volumeStrategy.WithCondition(() => allTwinUpdatesSent.IsPassed))
                                        .WithTimingStrategy<LinearTimingStrategy>(
                                                timing => timing.WithDelay(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10)))
                                        .StartDeliveringAsync(device);

            await twinDeliverable.StartDeliveringAsync(TwinDeliveryAction);
            await twinDeliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(600));

            allTwinUpdatesSent.Passed();

            await sendingMessagesTask;

            cloudClients = await GenericClientProvider.GetProvidedInstancesAsync(device.Identity.Id, CancellationToken.None);

            Assert.True(cloudClients.Count > 2); // just to make sure that the test destroys the client at least once
        }

        [RunnableInDebugOnly]
        public async Task TwinUpdateGetsReceivedAfterSingleDisposedExceptionDisconnects()
        {
            var cloudClientOpened = new TestMilestone();

            var twinDeliverable = TwinCollectionDeliverable
                                    .Create()
                                    .WithPackSize(SmallPack)
                                    .WithTimingStrategy<LinearTimingStrategy>(
                                        timing => timing.WithDelay(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(7)));

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                 .WithThrowTypeStrategy<SingleThrowTypeStrategy>(
                                                                        throwTypeStrategy => throwTypeStrategy.WithType<ObjectDisposedException>())
                                                                 .WithThrowTimingStrategy<RandomThrowTimingStrategy>(
                                                                        throwTimingStrategy => throwTimingStrategy.WithOddsToThrow(1))
                                                                 .WithOpenAction(cloudClientOpened.Passed)))
                                                         .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithDeviceProxy<AllGoodDeviceProxy>(
                                 proxy => proxy.WithDesiredPropertyUpdateAction(twinDeliverable.ConfirmDelivery))
                             .WithEdgeHub(edgeHub)
                             .Build();

            await cloudClientOpened.WaitAsync();

            var cloudClients = await GenericClientProvider.GetProvidedInstancesAsync(device.Identity.Id, Utils.CancelAfter(TimeSpan.FromSeconds(60)));
            var cloudClient = cloudClients.First() as FlakyClient;

            await Utils.WaitForAsync(() => cloudClient.HasUpdateDesiredPropertySubscription, Utils.CancelAfter(TimeSpan.FromSeconds(60)));

            Task TwinDeliveryAction(Shared.TwinCollection twin) =>
                    GenericClientProvider.ExecuteOnLastOneAsync(
                            device.Identity.Id,
                            client => (client as FlakyClient).UpdateDesiredProperty(twin),
                            Utils.CancelAfter(TimeSpan.FromSeconds(60)));

            var allTwinUpdatesSent = new TestMilestone();

            // sending only one message to let IClient throw DisposedException
            var sendingMessagesTask = DeviceMessageDeliverable
                                        .Create()
                                        .WithPackSize(1)
                                        .WithTimingStrategy<LinearTimingStrategy>(
                                                timing => timing.WithDelay(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10)))
                                        .StartDeliveringAsync(device);

            await twinDeliverable.StartDeliveringAsync(TwinDeliveryAction);
            await twinDeliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(600));

            allTwinUpdatesSent.Passed();

            await sendingMessagesTask;

            cloudClients = await GenericClientProvider.GetProvidedInstancesAsync(device.Identity.Id, CancellationToken.None);

            Assert.True(cloudClients.Count > 2); // just to make sure that the test destroys the client at least once
        }
    }
}
