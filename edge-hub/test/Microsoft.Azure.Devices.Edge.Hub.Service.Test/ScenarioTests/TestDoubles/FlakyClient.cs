// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Client;

    public class FlakyClient : AllGoodClient
    {
        private IMessageConverter<Exception> throwTypeStrategy;
        private IMessageConverter<bool> throwTimeStrategy;
        private ITimingStrategy timingStrategy;
        private Action<IReadOnlyCollection<Message>, Exception> throwingAction;

        // this instance created dynamically by CloudConnectionProvider, so a builder call will
        // transfer its state to every instance via this constructor
        public FlakyClient(
                    IMessageConverter<Exception> throwTypeStrategy,
                    IMessageConverter<bool> throwTimeStrategy,
                    ITimingStrategy timingStrategy,
                    Action<IReadOnlyCollection<Message>, Exception> throwingAction)
        {
            this.throwTypeStrategy = throwTypeStrategy;
            this.throwTimeStrategy = throwTimeStrategy;
            this.timingStrategy = timingStrategy;
            this.throwingAction = throwingAction;
        }

        public override async Task SendEventAsync(Message message)
        {
            await this.timingStrategy.DelayAsync();

            if (this.throwTimeStrategy.Convert(message))
            {
                var messageAsCollection = new Message[] { message };

                var toBeThrown = this.throwTypeStrategy.Convert(message);
                this.throwingAction(messageAsCollection, toBeThrown);
                throw toBeThrown;
            }

            await base.SendEventAsync(message);
        }

        public override async Task SendEventBatchAsync(IEnumerable<Message> messages)
        {
            await this.timingStrategy.DelayAsync();

            if (this.throwTimeStrategy.Convert(messages))
            {
                var messagesAsCollection = messages as IReadOnlyCollection<Message> ?? messages.ToArray();

                var toBeThrown = this.throwTypeStrategy.Convert(messages);
                this.throwingAction(messagesAsCollection, toBeThrown);
                throw toBeThrown;
            }

            await base.SendEventBatchAsync(messages);
        }
    }
}
