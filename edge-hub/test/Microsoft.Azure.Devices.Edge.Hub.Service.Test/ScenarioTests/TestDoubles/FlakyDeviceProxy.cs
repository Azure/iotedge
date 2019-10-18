// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public class FlakyDeviceProxy : AllGoodDeviceProxy
    {
        private IMessageConverter<Exception> throwTypeStrategy = SingleThrowTypeStrategy.Create().WithType<IotHubException>();
        private IMessageConverter<bool> throwTimeStrategy = RandomThrowTimeStrategy.Create().WithOddsToThrow(0.1);
        private ITimingStrategy timingStrategy = LinearTimingStrategy.Create().WithDelay(60, 40);
        private Action<IMessage> sendOutAction = _ => { };
        private Action<IMessage, Exception> throwingAction = (_, __) => { };

        public static FlakyDeviceProxy Create() => new FlakyDeviceProxy();

        public new FlakyDeviceProxy WithIdentity(IIdentity identity) => base.WithIdentity(identity) as FlakyDeviceProxy;
        public new FlakyDeviceProxy WithHubName(string iotHubName) => base.WithHubName(iotHubName) as FlakyDeviceProxy;

        public FlakyDeviceProxy WithException<T>()
            where T : Exception
        {
            this.throwTypeStrategy = SingleThrowTypeStrategy.Create().WithType<T>();
            return this;
        }

        public FlakyDeviceProxy WithThrowTypeStrategy(IMessageConverter<Exception> throwTypeStrategy)
        {
            this.throwTypeStrategy = throwTypeStrategy;
            return this;
        }

        public FlakyDeviceProxy WithThrowTypeStrategy<T>(Func<T, T> throwTypeStrategy)
            where T : IMessageConverter<Exception>, new()
        {
            this.throwTypeStrategy = throwTypeStrategy(new T());
            return this;
        }

        public FlakyDeviceProxy WithThrowTimeStrategy(IMessageConverter<bool> throwTimeStrategy)
        {
            this.throwTimeStrategy = throwTimeStrategy;
            return this;
        }

        public FlakyDeviceProxy WithThrowTimeStrategy<T>()
            where T : IMessageConverter<bool>, new()
        {
            this.throwTimeStrategy = new T();
            return this;
        }

        public FlakyDeviceProxy WithThrowTimeStrategy<T>(Func<T, T> throwTimeStrategy)
            where T : IMessageConverter<bool>, new()
        {
            this.throwTimeStrategy = throwTimeStrategy(new T());
            return this;
        }

        public FlakyDeviceProxy WithSendOutAction(Action<IMessage> action)
        {
            this.sendOutAction = action;
            return this;
        }

        public FlakyDeviceProxy WithThrowingAction(Action<IMessage, Exception> action)
        {
            this.throwingAction = action;
            return this;
        }

        public FlakyDeviceProxy WithSendDelay(TimeSpan coreDelay, TimeSpan delayVariance)
        {
            this.timingStrategy = LinearTimingStrategy.Create().WithDelay(coreDelay, delayVariance);
            return this;
        }

        public FlakyDeviceProxy WithSendDelay(int delayCore, int delayVariance)
        {
            this.timingStrategy = LinearTimingStrategy.Create().WithDelay(delayCore, delayVariance);
            return this;
        }

        public FlakyDeviceProxy WithSendDelayStrategy<T>(Func<T, T> timingStrategy)
            where T : ITimingStrategy, new()
        {
            this.timingStrategy = timingStrategy(new T());
            return this;
        }

        public override async Task SendMessageAsync(IMessage message, string input)
        {
            await this.timingStrategy.DelayAsync();

            if (this.throwTimeStrategy.Convert(message))
            {
                var toBeThrown = this.throwTypeStrategy.Convert(message);
                this.throwingAction(message, toBeThrown);
                throw toBeThrown;
            }

            this.sendOutAction(message);

            return;
        }
    }
}
