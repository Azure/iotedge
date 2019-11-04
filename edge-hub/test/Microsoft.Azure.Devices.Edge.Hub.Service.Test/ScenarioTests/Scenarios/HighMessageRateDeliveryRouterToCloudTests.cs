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
    public class HighMessageRateDeliveryRouterToCloudTests
    {
        private const int BigPack = 10000;
        private const int SmallPack = 100;

        [RunnableInDebugOnly]
        public async Task SendWithNoError()
        {
            var deliverable = RoutingMessageDeliverable
                                .Create()
                                .WithPackSize(BigPack)
                                .WithTimingStrategy<NoWaitTimingStrategy>();

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithThrowTimingStrategy<DoNotThrowStrategy>();

            var router = RouterBuilder
                            .Create()
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(10));

            deliverable.DeliveryJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task SendWithRetriableErrors()
        {
            var deliverable = RoutingMessageDeliverable
                                .Create()
                                .WithPackSize(BigPack)
                                .WithTimingStrategy<NoWaitTimingStrategy>();

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithException<Client.Exceptions.IotHubException>()
                                .WithThrowTimingStrategy<RandomThrowTimingStrategy>(throwing => throwing.WithOddsToThrow(0.2)); // 20% that throws

            var router = RouterBuilder
                            .Create()
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(10));

            deliverable.DeliveryJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task SendWithNonRetriableErrors()
        {
            var deliverable = RoutingMessageDeliverable
                                .Create()
                                .WithPackSize(BigPack)
                                .WithTimingStrategy<NoWaitTimingStrategy>();

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithThrowingAction((msg, ex) => deliverable.DontExpectDelivery(msg))
                                .WithException<Exception>()
                                .WithThrowTimingStrategy<RandomThrowTimingStrategy>(throwing => throwing.WithOddsToThrow(0.2)); // 20% that throws

            var router = RouterBuilder
                            .Create()
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(10));

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
                typeof(Client.Exceptions.UnauthorizedException)
            };

            var nonRetriableExceptions = new HashSet<Type>
            {
                typeof(Exception),
                typeof(NullReferenceException),
                typeof(ArgumentNullException)
            };

            var deliverable = RoutingMessageDeliverable
                                .Create()
                                .WithPackSize(BigPack)
                                .WithTimingStrategy<NoWaitTimingStrategy>();

            Action<IReadOnlyCollection<Core.IMessage>, Exception> handleExceptions =
                (msg, ex) =>
                {
                    if (!retriableExceptions.Contains(ex.GetType()))
                        deliverable.DontExpectDelivery(msg);
                };

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithThrowingAction(handleExceptions)
                                .WithThrowTypeStrategy<MultiThrowTypeStrategy>(
                                    exception => exception.WithExceptionSuite(retriableExceptions)
                                                          .WithExceptionSuite(nonRetriableExceptions))
                                .WithThrowTimingStrategy<RandomThrowTimingStrategy>(
                                    throwing => throwing.WithOddsToThrow(0.2));

            var router = RouterBuilder
                            .Create()
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(10));

            deliverable.DeliveryJournal.EnsureOrderedWithGaps();
        }

        [RunnableInDebugOnly]
        public async Task SendWithSlowNetwork()
        {
            var deliverable = RoutingMessageDeliverable
                                .Create()
                                .WithPackSize(BigPack)
                                .WithTimingStrategy<NoWaitTimingStrategy>();

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithSendDelay(1000, 500) // a packet is completed in 0.5-1.5 seconds
                                .WithThrowTimingStrategy<DoNotThrowStrategy>();

            var router = RouterBuilder
                            .Create()
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(20));

            deliverable.DeliveryJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task SendWithLaggingNetwork()
        {
            var deliverable = RoutingMessageDeliverable
                                .Create()
                                .WithPackSize(SmallPack) // careful, this test waits a lot
                                .WithTimingStrategy<NoWaitTimingStrategy>();

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithThrowTimingStrategy<DoNotThrowStrategy>()
                                .WithSendDelayStrategy<RandomLaggingTimingStrategy>(
                                     delay => delay.WithDelay(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(4))
                                                   .WithOddsToGetStuck(0.05) // lag at every ~20th send in average
                                                   .WithBaseStrategy<LinearTimingStrategy>(
                                                        baseDelay => baseDelay.WithDelay(300, 100))); // 0.2-0.4 sec normal delay

            var router = RouterBuilder
                            .Create()
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task SendBigMessages()
        {
            var deliverable = RoutingMessageDeliverable
                                .Create()
                                .WithPackSize(SmallPack)
                                .WithDeliverableGeneratingStrategy<SimpleRoutingMessageGeneratingStrategy>(
                                    message => message.WithBodySize((int)Core.Constants.MaxMessageSize / 4, (int)Core.Constants.MaxMessageSize))
                                .WithTimingStrategy<NoWaitTimingStrategy>();

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithThrowTimingStrategy<DoNotThrowStrategy>();

            var router = RouterBuilder
                            .Create()
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(20));

            deliverable.DeliveryJournal.EnsureOrdered();
        }
    }
}
