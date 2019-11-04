// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    [Scenario]
    public class IdleTimeoutRoutingHubToCloudProxyTests
    {
        private const int BigPack = 10000;
        private const int SmallPack = 100;
        private const int TinyPack = 20;

        [RunnableInDebugOnly]
        public async Task RecoverAfterCloudConnectionIdle()
        {
            var closeCounter = 0;
            void CloseCounterAction() => Interlocked.Increment(ref closeCounter);

            var deliverable = DeviceMessageDeliverable
                                .Create()
                                .WithPackSize(SmallPack)
                                .WithTimingStrategy<LinearTimingStrategy>(
                                    timing => timing.WithDelay(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(15)));

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider(
                                                             connectionProvider => connectionProvider
                                                                                      .WithCloseOnIdle(true)
                                                                                      .WithCloudConnectionIdleTimeout(TimeSpan.FromSeconds(20))
                                                                                      .WithClientProvider<FlakyClientBuilder>(
                                                                                          clientProvider => clientProvider
                                                                                                               .WithThrowTimingStrategy<DoNotThrowStrategy>()
                                                                                                               .WithSendEventAction(deliverable.ConfirmDelivery)
                                                                                                               .WithCloseAction(CloseCounterAction))))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithNoSubscription()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrdered();

            Assert.True(Volatile.Read(ref closeCounter) > 0); // to be sure that it closed on idle, adjust timing if failed
        }

        [RunnableInDebugOnly]
        public async Task RecoverAfterCloudConnectionIdleWithOpenThrows()
        {
            var closeCounter = 0;
            void CloseCounterAction() => Interlocked.Increment(ref closeCounter);

            var randomThrowGenerator = RandomThrowGenerator<IotHubCommunicationException>.Create().WithOdds(0.5);
            void OpenThrowingAction() => randomThrowGenerator.ThrowOrNot();

            var deliverable = DeviceMessageDeliverable
                                .Create()
                                .WithPackSize(SmallPack)
                                .WithTimingStrategy<LinearTimingStrategy>(
                                    timing => timing.WithDelay(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(15)));

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider(
                                                             connectionProvider => connectionProvider
                                                                                      .WithCloseOnIdle(true)
                                                                                      .WithCloudConnectionIdleTimeout(TimeSpan.FromSeconds(20))
                                                                                      .WithClientProvider<FlakyClientBuilder>(
                                                                                          clientProvider => clientProvider
                                                                                                               .WithThrowTimingStrategy<DoNotThrowStrategy>()
                                                                                                               .WithSendEventAction(deliverable.ConfirmDelivery)
                                                                                                               .WithCloseAction(CloseCounterAction)
                                                                                                               .WithOpenAction(OpenThrowingAction))))
                             .Build();

            var device = DeviceBuilder
                             .Create()
                             .WithNoSubscription()
                             .WithEdgeHub(edgeHub)
                             .Build();

            await deliverable.StartDeliveringAsync(device);
            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(60));

            deliverable.DeliveryJournal.EnsureOrdered();

            Assert.True(Volatile.Read(ref closeCounter) > 0); // to be sure that it closed on idle, adjust timing if failed
            Assert.True(randomThrowGenerator.ThrowCount > 0); // to be sure that we had failed open, increase the odds if failed
        }

        [RunnableInDebugOnly]
        public async Task RecoverFromClientSdkNoConnectionCloseFails()
        {
            var idleTimeout = TimeSpan.FromSeconds(20);

            var sendTrigger = new AutoResetEvent(false);
            var networkBackTrigger = new ManualResetEvent(false);

            var idleTriggered = new TestMilestone();
            var closeThrew = new TestMilestone();

            var deliverable = DeviceMessageDeliverable
                        .Create()
                        .WithPackSize(2)
                        .WithTimingStrategy<EventBasedTimingStrategy>(
                            timing => timing.WithTrigger(sendTrigger));

            // When no network, CloseAsync would get stuck and throw a timeout when the net comes back
            // this test becomes obsolate if SDK team changes this behavior
            void CloseAction()
            {
                idleTriggered.Passed();
                networkBackTrigger.WaitOne();

                closeThrew.Passed();
                throw new TimeoutException("Test exception - simulating ModuleClient.CloseAsyn() when network comes back");
            }

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider(
                                                             connectionProvider => connectionProvider
                                                                                      .WithCloseOnIdle(true)
                                                                                      .WithCloudConnectionIdleTimeout(idleTimeout)
                                                                                      .WithClientProvider<AllGoodClientBuilder>(
                                                                                          clientProvider => clientProvider
                                                                                                               .WithSendEventAction(deliverable.ConfirmDelivery)
                                                                                                               .WithCloseAction(CloseAction))))
                             .Build();

            var device = DeviceBuilder
                 .Create()
                 .WithNoSubscription()
                 .WithEdgeHub(edgeHub)
                 .Build();

            _ = deliverable.StartDeliveringAsync(device);

            sendTrigger.Set(); // send one throught to open, so idle can close it

            await idleTriggered.WaitAsync();
            await Task.Delay(TimeSpan.FromSeconds(2));

            networkBackTrigger.Set();

            await closeThrew.WaitAsync();
            await Task.Delay(TimeSpan.FromSeconds(2));

            sendTrigger.Set();

            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(1));
        }

        [RunnableInDebugOnly]
        public async Task RecoverFromClientSdkNoConnectionIdleAndThrow()
        {
            var idleTimeout = TimeSpan.FromSeconds(20);

            var sendTrigger = new AutoResetEvent(false);
            var networkBackTrigger = new ManualResetEvent(false);

            var idleTriggered = new TestMilestone();
            var closeThrew = new TestMilestone();
            var sendThrew = new TestMilestone();

            var deliverable = DeviceMessageDeliverable
                        .Create()
                        .WithPackSize(3)
                        .WithTimingStrategy<EventBasedTimingStrategy>(
                            timing => timing.WithTrigger(sendTrigger));

            // setting up the SDK behavior. When no network, SDK calls block, and when the network comes back, it throws
            // and exception. This test simulates a network outage (by holding back SDK calls) and checks that the
            // throwing methods are still followed by recovery. This test might become obsolate if they change SDK behavior.
            var messageSentToOpen = 0;
            var messageGotStuck = 0;

            void CloseAction()
            {
                idleTriggered.Passed();
                networkBackTrigger.WaitOne();

                closeThrew.Passed();
                throw new TimeoutException("Test exception - simulating DeviceClient.Close() when network comes back");
            }

            void SendAction(IReadOnlyCollection<Client.Message> messages)
            {
                if (Interlocked.CompareExchange(ref messageSentToOpen, 1, 0) == 0)
                {
                    deliverable.ConfirmDelivery(messages); // let the very first message through which just opens the connection
                }

                if (Interlocked.CompareExchange(ref messageGotStuck, 1, 0) == 0)
                {
                    networkBackTrigger.WaitOne();

                    sendThrew.Passed();
                    throw new IotHubCommunicationException("Test exception - simulating DeviceClient.Send() when network comes back");
                }

                deliverable.ConfirmDelivery(messages);
            }

            var edgeHub = EdgeHubBuilder
                             .Create()
                             .WithConnectionManager(
                                 connectionManager => connectionManager
                                                         .WithNoDevices()
                                                         .WithCloudConnectionProvider(
                                                             connectionProvider => connectionProvider
                                                                                      .WithCloseOnIdle(true)
                                                                                      .WithCloudConnectionIdleTimeout(idleTimeout)
                                                                                      .WithClientProvider<AllGoodClientBuilder>(
                                                                                          clientProvider => clientProvider
                                                                                                               .WithSendEventAction(SendAction)
                                                                                                               .WithCloseAction(CloseAction))))
                             .Build();

            var device = DeviceBuilder
                 .Create()
                 .WithNoSubscription()
                 .WithEdgeHub(edgeHub)
                 .Build();

            _ = deliverable.StartDeliveringAsync(device);

            sendTrigger.Set(); // send one throught to open, so idle can close it

            await idleTriggered.WaitAsync();
            await Task.Delay(TimeSpan.FromSeconds(2));

            sendTrigger.Set(); // this one will get stuck

            networkBackTrigger.Set();

            await closeThrew.WaitAsync();
            await sendThrew.WaitAsync();
            await Task.Delay(TimeSpan.FromSeconds(2)); // let things settle down a bit

            sendTrigger.Set();

            await deliverable.WaitTillAllDeliveredAsync(TimeSpan.FromMinutes(1));
        }
    }
}
