// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IDeliveryStrategy<TSend, TRcv, TMedium>
    {
        IDeliverableGeneratingStrategy<TSend> DefaultDeliverableGeneratingStrategy { get; }

        string GetDeliverableId(TSend deliverable);
        string GetDeliverableId(TRcv deliverable);

        Task SendDeliverable(TMedium medium, TSend deliverable);
    }

    public class StandardDeliverable<TSend, TRcv, TMedium>
    {
        private object lockObject = new object();

        private ITimingStrategy timingStrategy = new LinearTimingStrategy();
        private IDeliveryVolumeStrategy deliveryVolumeStrategy = ConstantSizeVolumeStrategy.Create().WithPackSize(100);

        private IDeliveryStrategy<TSend, TRcv, TMedium> deliveryStrategy;
        private IDeliverableGeneratingStrategy<TSend> deliverableGeneratingStrategy;

        private List<TSend> sentJournal = new List<TSend>();
        private List<TRcv> deliveryJournal = new List<TRcv>();
        private HashSet<string> pendingDeliverables = new HashSet<string>();

        public static StandardDeliverable<TSend, TRcv, TMedium> Create(IDeliveryStrategy<TSend, TRcv, TMedium> deliveryStrategy) => new StandardDeliverable<TSend, TRcv, TMedium>(deliveryStrategy);

        public StandardDeliverable(IDeliveryStrategy<TSend, TRcv, TMedium> deliveryStrategy)
        {
            this.deliveryStrategy = deliveryStrategy;
            this.deliverableGeneratingStrategy = deliveryStrategy.DefaultDeliverableGeneratingStrategy;
        }

        public StandardDeliverable<TSend, TRcv, TMedium> WithVolumeStrategy(IDeliveryVolumeStrategy deliveryVolumeStrategy)
        {
            this.deliveryVolumeStrategy = deliveryVolumeStrategy;
            return this;
        }

        public StandardDeliverable<TSend, TRcv, TMedium> WithVolumeStrategy<T>(Func<T, T> strategyDecorator)
            where T : IDeliveryVolumeStrategy, new()
        {
            this.deliveryVolumeStrategy = strategyDecorator(new T());
            return this;
        }

        public StandardDeliverable<TSend, TRcv, TMedium> WithPackSize(int packSize)
        {
            this.deliveryVolumeStrategy = ConstantSizeVolumeStrategy.Create().WithPackSize(packSize);
            return this;
        }

        public StandardDeliverable<TSend, TRcv, TMedium> WithTimingStrategy<T>()
            where T : ITimingStrategy, new()
        {
            this.timingStrategy = new T();
            return this;
        }

        public StandardDeliverable<TSend, TRcv, TMedium> WithTimingStrategy<T>(Func<T, T> strategyDecorator)
            where T : ITimingStrategy, new()
        {
            this.timingStrategy = strategyDecorator(new T());
            return this;
        }

        public StandardDeliverable<TSend, TRcv, TMedium> WithDeliverableGeneratingStrategy<T>(Func<T, T> strategyDecorator)
            where T : IDeliverableGeneratingStrategy<TSend>, new()
        {
            this.deliverableGeneratingStrategy = strategyDecorator(new T());
            return this;
        }

        public void DontExpectDelivery(TRcv deliverable) => this.DontExpectDelivery(new[] { deliverable });

        // call this when we know that deliverables won't be sent (e.g. dropped)
        public void DontExpectDelivery(IReadOnlyCollection<TRcv> deliverables)
        {
            lock (this.lockObject)
            {
                foreach (var msg in deliverables)
                {
                    this.pendingDeliverables.Remove(this.deliveryStrategy.GetDeliverableId(msg));
                }
            }
        }

        public void ConfirmDelivery(TRcv deliverable) => this.ConfirmDelivery(new[] { deliverable });

        public void ConfirmDelivery(IReadOnlyCollection<TRcv> deliverables)
        {
            lock (this.lockObject)
            {
                this.deliveryJournal.AddRange(deliverables);

                foreach (var msg in deliverables)
                {
                    this.pendingDeliverables.Remove(this.deliveryStrategy.GetDeliverableId(msg));
                }
            }
        }

        // to make these usable for high number of deliverables, these are not copied (nor locked)
        // the intended usage of these to getters are to do checks AFTER the test has run
        public IReadOnlyList<TSend> SentJournal => this.sentJournal;
        public IReadOnlyList<TRcv> DeliveryJournal => this.deliveryJournal;

        public async Task StartDeliveringAsync(TMedium device)
        {
            while (this.deliveryVolumeStrategy.TakeOneIfAny())
            {
                await this.timingStrategy.DelayAsync();

                var deliverable = this.deliverableGeneratingStrategy.Next();

                lock (this.lockObject)
                {
                    this.sentJournal.Add(deliverable);
                    this.pendingDeliverables.Add(this.deliveryStrategy.GetDeliverableId(deliverable));
                }

                await this.deliveryStrategy.SendDeliverable(device, deliverable);
            }
        }

        public Task WaitTillAllDeliveredAsync(TimeSpan cancelAfter)
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(cancelAfter);

            return this.WaitTillAllDeliveredAsync(tokenSource.Token);
        }

        public async Task WaitTillAllDeliveredAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                lock (this.lockObject)
                {
                    if (!this.deliveryVolumeStrategy.HasMore && this.pendingDeliverables.Count == 0)
                    {
                        return;
                    }
                }

                await Task.Delay(1000);
            }
        }
    }
}
