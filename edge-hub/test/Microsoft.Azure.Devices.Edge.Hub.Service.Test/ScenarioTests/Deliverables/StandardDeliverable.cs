namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{    
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core;

    public class StandardDeliverable
    {
        private object lockObject = new object();

        private int packSize = 100;
        private ITimingStrategy timingStrategy = new LinearTimingStrategy();
        private IMessageGeneratingStrategy messageGeneratingStrategy = new SimpleMessageGeneratingStrategy();

        private List<Message> sentJournal = new List<Message>();
        private List<Hub.Core.IMessage> deliveredJournal = new List<Hub.Core.IMessage>();
        private HashSet<string> pendingMessages = new HashSet<string>();

        public static StandardDeliverable Create() => new StandardDeliverable();

        public StandardDeliverable WithPackSize(int packSize)
        {
            this.packSize = packSize;
            return this;
        }

        public StandardDeliverable WithTimingStrategy<T>(Func<T, T> timingStrategy)
            where T : ITimingStrategy, new()
        {
            this.timingStrategy = timingStrategy(new T());
            return this;
        }

        public StandardDeliverable WithMessageGeneratingStrategy<T>(Func<T, T> messageGeneratingStrategy)
            where T : IMessageGeneratingStrategy, new()
        {
            this.messageGeneratingStrategy = messageGeneratingStrategy(new T());
            return this;
        }

        public void ConfirmDelivery(IReadOnlyCollection<Hub.Core.IMessage> messages)
        {
            lock (this.lockObject)
            {
                this.deliveredJournal.AddRange(messages);

                foreach (var msg in messages)
                {
                    this.pendingMessages.Remove(msg.SystemProperties[Core.SystemProperties.EdgeMessageId]);
                }
            }
        }

        // to make these usable for high number of messages, this are not copied (nor locked)
        // the intended usage of these to getters are to do checks AFTER the test has run
        public IReadOnlyCollection<Message> SentJournal => this.sentJournal;
        public List<Hub.Core.IMessage> DeliveredJournal => this.deliveredJournal;

        public async Task StartDeliveringAsync(Router router)
        {
            for (var i = 0; i < this.packSize; i++)
            {
                await this.timingStrategy.DelayAsync();

                var message = this.messageGeneratingStrategy.Next();

                lock (this.lockObject)
                {
                    this.sentJournal.Add(message);
                    this.pendingMessages.Add(message.SystemProperties[Core.SystemProperties.EdgeMessageId]);
                }

                await router.RouteAsync(message);
            }
        }

        public async Task WaitTillAllDeliveredAsync(CancellationToken token)
        {
            while(true)
            {
                token.ThrowIfCancellationRequested();

                lock (this.lockObject)
                {
                    if (this.deliveredJournal.Count >= this.packSize)
                    {
                        if (this.pendingMessages.Count == 0)
                            return;
                    }
                }

                await Task.Delay(1000);
            }
        }
    }
}
