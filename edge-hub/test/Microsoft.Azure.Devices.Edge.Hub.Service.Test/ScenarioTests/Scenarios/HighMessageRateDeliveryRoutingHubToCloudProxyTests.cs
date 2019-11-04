// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    [Scenario]
    public class HighMessageRateDeliveryRoutingHubToCloudProxyTests
    {
        private const int BigPack = 10000;
        private const int SmallPack = 100;

        [RunnableInDebugOnly]
        public async Task SendWithNoError()
        {
            var deliverable = DeviceMessageDeliverable
                    .Create()
                    .WithPackSize(BigPack)
                    .WithTimingStrategy<NoWaitTimingStrategy>();

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                                    .WithThrowTimingStrategy<DoNotThrowStrategy>()
                                                                                    .WithSendEventAction(deliverable.ConfirmDelivery)))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task SendWithRetriableErrors()
        {
            var deliverable = DeviceMessageDeliverable
                    .Create()
                    .WithPackSize(BigPack)
                    .WithTimingStrategy<NoWaitTimingStrategy>();

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                                    .WithException<Client.Exceptions.IotHubException>()
                                                                                    .WithThrowTimingStrategy<RandomThrowTimingStrategy>(throwing => throwing.WithOddsToThrow(0.2)) // 20% that throws
                                                                                    .WithSendEventAction(deliverable.ConfirmDelivery)))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task SendWithNonRetriableErrors()
        {
            var deliverable = DeviceMessageDeliverable
                    .Create()
                    .WithPackSize(BigPack)
                    .WithTimingStrategy<NoWaitTimingStrategy>();

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                                    .WithException<Exception>()
                                                                                    .WithThrowTimingStrategy<RandomThrowTimingStrategy>(throwing => throwing.WithOddsToThrow(0.2)) // 20% that throws
                                                                                    .WithSendEventAction(deliverable.ConfirmDelivery)
                                                                                    .WithThrowingAction((msg, ex) => deliverable.DontExpectDelivery(msg))))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrderedWithGaps();
        }

        [RunnableInDebugOnly]
        public async Task SendWithMixedErrors()
        {
            var retriableExceptions = new HashSet<Type>
            {
                typeof(TimeoutException),
                typeof(IOException),
                typeof(Client.Exceptions.IotHubException),
            };

            var nonRetriableExceptions = new HashSet<Type>
            {
                typeof(Exception),
                typeof(NullReferenceException),
                typeof(ArgumentNullException)
            };

            var deliverable = DeviceMessageDeliverable
                                .Create()
                                .WithPackSize(BigPack)
                                .WithTimingStrategy<NoWaitTimingStrategy>();

            Action<IReadOnlyCollection<Client.Message>, Exception> handleExceptions =
                (msg, ex) =>
                {
                    if (!retriableExceptions.Contains(ex.GetType()))
                    {
                        deliverable.DontExpectDelivery(msg);
                    }
                };

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                                    .WithSendEventAction(deliverable.ConfirmDelivery)
                                                                                    .WithThrowingAction(handleExceptions)
                                                                                    .WithThrowTypeStrategy<MultiThrowTypeStrategy>(
                                                                                        exception => exception.WithExceptionSuite(retriableExceptions)
                                                                                                              .WithExceptionSuite(nonRetriableExceptions))
                                                                                    .WithThrowTimingStrategy<RandomThrowTimingStrategy>(
                                                                                        throwing => throwing.WithOddsToThrow(0.2))))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrderedWithGaps();
        }

        [RunnableInDebugOnly]
        public async Task SendWithSlowNetwork()
        {
            var deliverable = DeviceMessageDeliverable
                    .Create()
                    .WithPackSize(BigPack)
                    .WithTimingStrategy<NoWaitTimingStrategy>();

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                                    .WithThrowTimingStrategy<DoNotThrowStrategy>()
                                                                                    .WithSendDelay(1000, 500) // a packet is completed in 0.5-1.5 seconds
                                                                                    .WithSendEventAction(deliverable.ConfirmDelivery)))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task SendWithLaggingNetwork()
        {
            var deliverable = DeviceMessageDeliverable
                    .Create()
                    .WithPackSize(SmallPack)
                    .WithTimingStrategy<NoWaitTimingStrategy>();

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                                    .WithThrowTimingStrategy<DoNotThrowStrategy>()
                                                                                    .WithSendDelayStrategy<RandomLaggingTimingStrategy>(
                                                                                         delay => delay.WithDelay(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(4))
                                                                                                       .WithOddsToGetStuck(0.05) // lag at every ~20th send in average
                                                                                                       .WithBaseStrategy<LinearTimingStrategy>(
                                                                                                            baseDelay => baseDelay.WithDelay(300, 100))) // 0.2-0.4 sec normal delay
                                                                                    .WithSendEventAction(deliverable.ConfirmDelivery)))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(120));

            deliverable.DeliveryJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task SendBigMessages()
        {
            var deliverable = DeviceMessageDeliverable
                    .Create()
                    .WithPackSize(BigPack)
                    .WithDeliverableGeneratingStrategy<SimpleDeviceMessageGeneratingStrategy>(
                          message => message.WithBodySize((int)Core.Constants.MaxMessageSize / 4, (int)Core.Constants.MaxMessageSize))
                    .WithTimingStrategy<NoWaitTimingStrategy>();

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider<FlakyClientBuilder>(
                                                             clientProvider => clientProvider
                                                                                    .WithThrowTimingStrategy<DoNotThrowStrategy>()
                                                                                    .WithSendEventAction(deliverable.ConfirmDelivery)))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrdered();
        }
    }
}
