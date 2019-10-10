namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    [Unit]
    public class MessagingRateTests
    {
        [RunnableInDebugOnly]
        public async Task HighRateMessagesWithNoError()
        {
            var deliverable = StandardDeliverable
                                .Create()
                                .WithPackSize(10000)
                                .WithTimingStrategy<LinearTimingStrategy>(timing => timing.WithDelay(0,0)); // rapid fire

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithRandomThrowingStrategy(0); // 0% that throws

            var router = RouterBuilder
                            .Create()                            
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            var timeout = new CancellationTokenSource();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(timeout.Token);

            timeout.CancelAfter(TimeSpan.FromMinutes(10));

            deliverable.DeliveredJournal.EnsureOrdered();
        }

        [RunnableInDebugOnly]
        public async Task HighRateMessagesWithRetriableErrors()
        {
            var deliverable = StandardDeliverable
                                .Create()
                                .WithPackSize(10000)
                                .WithTimingStrategy<LinearTimingStrategy>(timing => timing.WithDelay(0, 0)); // rapid fire

            var cloudProxy = FlakyCloudProxy
                                .Create()
                                .WithSendOutAction(deliverable.ConfirmDelivery)
                                .WithException<Client.Exceptions.IotHubException>()
                                .WithRandomThrowingStrategy(0.2); // 20% that throws

            var router = RouterBuilder
                            .Create()
                            .WithRoute(route => route.WithProxyGetter(cloudProxy.CreateCloudProxyGetter()))
                            .Build();

            var timeout = new CancellationTokenSource();

            await deliverable.StartDeliveringAsync(router);
            await deliverable.WaitTillAllDeliveredAsync(timeout.Token);

            timeout.CancelAfter(TimeSpan.FromMinutes(10));

            deliverable.DeliveredJournal.EnsureOrdered();
        }

    }
}
