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
        private IMessageConverter<Exception> exceptionStrategy = SimpleExceptionStrategy.Create().WithType<IotHubException>();
        private IMessageConverter<bool> throwingStrategy = RandomThrowingStrategy.Create().WithOddsToThrow(0.1);
        private Action<IReadOnlyCollection<IMessage>> sendOutAction = _ => { };
        private Action<IReadOnlyCollection<IMessage>, Exception> throwingAction = (_,__) => { };

        private int delayCore = 200;
        private int delayVariance = 100;

        private Random rnd = new Random();

        public static FlakyCloudProxy Create()
        {
            return new FlakyCloudProxy();
        }

        public FlakyCloudProxy WithException<T>()
            where T : Exception
        {
            this.exceptionStrategy = SimpleExceptionStrategy.Create().WithType<T>();
            return this;
        }

        public FlakyCloudProxy WithException(IMessageConverter<Exception> exceptionFactory)
        {
            this.exceptionStrategy = exceptionFactory;
            return this;
        }

        public FlakyCloudProxy WithRandomThrowingStrategy(double oddsToThrow)
        {
            this.throwingStrategy = RandomThrowingStrategy.Create().WithOddsToThrow(oddsToThrow);
            return this;
        }

        public FlakyCloudProxy WithThrowingStrategy(IMessageConverter<bool> errorStrategy)
        {
            this.throwingStrategy = errorStrategy;
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

        public FlakyCloudProxy WithSendDelay(int delayCore, int delayVariance)
        {
            this.delayCore = delayCore;
            this.delayVariance = delayVariance;

            return this;
        }

        public Func<string, Task<Option<ICloudProxy>>> CreateCloudProxyGetter()
        {
            return _ => Task.FromResult(Option.Some(this as ICloudProxy));
        }

        public override async Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages)
        {
            await this.Delay();

            var messagesAsCollection = inputMessages as IReadOnlyCollection<IMessage> ?? inputMessages.ToArray();
            
            if (this.throwingStrategy.Convert(inputMessages))
            {
                var toBeThrown = this.exceptionStrategy.Convert(inputMessages);
                this.throwingAction(messagesAsCollection, toBeThrown);
                throw toBeThrown;
            }

            this.sendOutAction(messagesAsCollection);

            return;
        }

        public override async Task SendMessageAsync(IMessage message)
        {
            await this.Delay();

            var messageAsCollection = new IMessage[] { message };

            if (this.throwingStrategy.Convert(message))
            {
                var toBeThrown = this.exceptionStrategy.Convert(message);
                this.throwingAction(messageAsCollection, toBeThrown);
                throw toBeThrown;
            }

            this.sendOutAction(messageAsCollection);

            return;
        }

        private async Task Delay()
        {
            var delayMs = default(int);

            lock (this.rnd)
            {
                delayMs = this.rnd.Next(this.delayCore - this.delayVariance, this.delayCore + this.delayVariance);
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
            else
            {
                await Task.Yield();
            }
        }
    }
}
