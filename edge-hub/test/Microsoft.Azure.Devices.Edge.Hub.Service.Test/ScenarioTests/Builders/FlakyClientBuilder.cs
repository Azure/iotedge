// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public class FlakyClientBuilder : AllGoodClientBuilder
    {
        private IMessageConverter<Exception> throwTypeStrategy = SingleThrowTypeStrategy.Create().WithType<IotHubException>();
        private IMessageConverter<bool> throwTimeStrategy = RandomThrowTimingStrategy.Create().WithOddsToThrow(0.1);
        private ITimingStrategy timingStrategy = LinearTimingStrategy.Create().WithDelay(200, 100);
        private Action<IReadOnlyCollection<Message>, Exception> throwingAction = (_, __) => { };

        public FlakyClientBuilder WithException<T>()
            where T : Exception
        {
            this.throwTypeStrategy = SingleThrowTypeStrategy.Create().WithType<T>();
            return this;
        }

        public FlakyClientBuilder WithThrowTypeStrategy(IMessageConverter<Exception> throwTypeStrategy)
        {
            this.throwTypeStrategy = throwTypeStrategy;
            return this;
        }

        public FlakyClientBuilder WithThrowTypeStrategy<T>(Func<T, T> throwTypeStrategyDecorator)
            where T : IMessageConverter<Exception>, new()
        {
            this.throwTypeStrategy = throwTypeStrategyDecorator(new T());
            return this;
        }

        public FlakyClientBuilder WithThrowTimingStrategy(IMessageConverter<bool> throwTimeStrategy)
        {
            this.throwTimeStrategy = throwTimeStrategy;
            return this;
        }

        public FlakyClientBuilder WithThrowTimingStrategy<T>()
            where T : IMessageConverter<bool>, new()
        {
            this.throwTimeStrategy = new T();
            return this;
        }

        public FlakyClientBuilder WithThrowTimingStrategy<T>(Func<T, T> throwTimeStrategyDecorator)
            where T : IMessageConverter<bool>, new()
        {
            this.throwTimeStrategy = throwTimeStrategyDecorator(new T());
            return this;
        }

        public FlakyClientBuilder WithThrowingAction(Action<IReadOnlyCollection<Message>, Exception> action)
        {
            this.throwingAction = action;
            return this;
        }

        public FlakyClientBuilder WithSendDelay(TimeSpan coreDelay, TimeSpan delayVariance)
        {
            this.timingStrategy = LinearTimingStrategy.Create().WithDelay(coreDelay, delayVariance);
            return this;
        }

        public FlakyClientBuilder WithSendDelay(int delayCoreMs, int delayVarianceMs)
        {
            this.timingStrategy = LinearTimingStrategy.Create().WithDelay(delayCoreMs, delayVarianceMs);
            return this;
        }

        public FlakyClientBuilder WithSendDelayStrategy<T>(Func<T, T> timingStrategyDecorator)
            where T : ITimingStrategy, new()
        {
            this.timingStrategy = timingStrategyDecorator(new T());
            return this;
        }

        public new FlakyClientBuilder WithOpenAction(Action action)
        {
            base.WithOpenAction(action);
            return this;
        }

        public new FlakyClientBuilder WithCloseAction(Action action)
        {
            base.WithCloseAction(action);
            return this;
        }

        public new FlakyClientBuilder WithSendEventAction(Action<IReadOnlyCollection<Client.Message>> action)
        {
            base.WithSendEventAction(action);
            return this;
        }

        public override CloudProxy.IClient Build(IIdentity identity)
        {
            var result = new FlakyClient(this.throwTypeStrategy, this.throwTimeStrategy, this.timingStrategy, this.throwingAction);

            this.AddHooks(result);

            return result;
        }
    }
}
