// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    public class FlakyCloudProxy : AllGoodCloudProxy
    {
        private IMessageConverter<Exception> throwTypeStrategy = SingleThrowTypeStrategy.Create().WithType<IotHubException>();
        private IMessageConverter<bool> throwTimeStrategy = RandomThrowTimeStrategy.Create().WithOddsToThrow(0.1);
        private ITimingStrategy timingStrategy = LinearTimingStrategy.Create().WithDelay(200, 100);
        private Action<IReadOnlyCollection<IMessage>> sendOutAction = _ => { };
        private Action<IReadOnlyCollection<IMessage>, Exception> throwingAction = (_, __) => { };

        public static FlakyCloudProxy Create() => new FlakyCloudProxy();

        public FlakyCloudProxy WithException<T>()
            where T : Exception
        {
            this.throwTypeStrategy = SingleThrowTypeStrategy.Create().WithType<T>();
            return this;
        }

        public FlakyCloudProxy WithThrowTypeStrategy(IMessageConverter<Exception> throwTypeStrategy)
        {
            this.throwTypeStrategy = throwTypeStrategy;
            return this;
        }

        public FlakyCloudProxy WithThrowTypeStrategy<T>(Func<T, T> throwTypeStrategy)
            where T : IMessageConverter<Exception>, new()
        {
            this.throwTypeStrategy = throwTypeStrategy(new T());
            return this;
        }

        public FlakyCloudProxy WithThrowTimeStrategy(IMessageConverter<bool> throwTimeStrategy)
        {
            this.throwTimeStrategy = throwTimeStrategy;
            return this;
        }

        public FlakyCloudProxy WithThrowTimeStrategy<T>()
            where T : IMessageConverter<bool>, new()
        {
            this.throwTimeStrategy = new T();
            return this;
        }

        public FlakyCloudProxy WithThrowTimeStrategy<T>(Func<T, T> throwTimeStrategy)
            where T : IMessageConverter<bool>, new()
        {
            this.throwTimeStrategy = throwTimeStrategy(new T());
            return this;
        }

        public FlakyCloudProxy WithSendOutAction(Action<IReadOnlyCollection<IMessage>> action)
        {
            this.sendOutAction = action;
            return this;
        }

        public FlakyCloudProxy WithThrowingAction(Action<IReadOnlyCollection<IMessage>, Exception> action)
        {
            this.throwingAction = action;
            return this;
        }

        public FlakyCloudProxy WithSendDelay(TimeSpan coreDelay, TimeSpan delayVariance)
        {
            this.timingStrategy = LinearTimingStrategy.Create().WithDelay(coreDelay, delayVariance);
            return this;
        }

        public FlakyCloudProxy WithSendDelay(int delayCore, int delayVariance)
        {
            this.timingStrategy = LinearTimingStrategy.Create().WithDelay(delayCore, delayVariance);
            return this;
        }

        public FlakyCloudProxy WithSendDelayStrategy<T>(Func<T, T> timingStrategy)
            where T : ITimingStrategy, new()
        {
            this.timingStrategy = timingStrategy(new T());
            return this;
        }

        public Func<string, Task<Option<ICloudProxy>>> CreateCloudProxyGetter()
        {
            return _ => Task.FromResult(Option.Some(this as ICloudProxy));
        }

        public override async Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages)
        {
            await this.timingStrategy.DelayAsync();

            var messagesAsCollection = inputMessages as IReadOnlyCollection<IMessage> ?? inputMessages.ToArray();

            if (this.throwTimeStrategy.Convert(inputMessages))
            {
                var toBeThrown = this.throwTypeStrategy.Convert(inputMessages);
                this.throwingAction(messagesAsCollection, toBeThrown);
                throw toBeThrown;
            }

            this.sendOutAction(messagesAsCollection);

            return;
        }

        public override async Task SendMessageAsync(IMessage message)
        {
            await this.timingStrategy.DelayAsync();

            var messageAsCollection = new IMessage[] { message };

            if (this.throwTimeStrategy.Convert(message))
            {
                var toBeThrown = this.throwTypeStrategy.Convert(message);
                this.throwingAction(messageAsCollection, toBeThrown);
                throw toBeThrown;
            }

            this.sendOutAction(messageAsCollection);

            return;
        }
    }
}
