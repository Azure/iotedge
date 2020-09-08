// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading;

    public interface IDeliveryVolumeStrategy
    {
        bool HasMore { get; }
        bool TakeOneIfAny();
    }

    public class ConstantSizeVolumeStrategy : IDeliveryVolumeStrategy
    {
        private int packSize = 1;

        public static ConstantSizeVolumeStrategy Create() => new ConstantSizeVolumeStrategy();

        public ConstantSizeVolumeStrategy WithPackSize(int packSize)
        {
            this.packSize = packSize;
            return this;
        }

        public bool HasMore => Volatile.Read(ref this.packSize) > 0;

        public bool TakeOneIfAny()
        {
            var snapshot = Volatile.Read(ref this.packSize);

            while (true)
            {
                if (snapshot == 0)
                {
                    return false;
                }

                if (snapshot == Interlocked.CompareExchange(ref this.packSize, snapshot - 1, snapshot))
                {
                    return true;
                }
            }
        }
    }

    public class ConditionBasedVolumeStrategy : IDeliveryVolumeStrategy
    {
        private Func<bool> conditionToStop = () => false;

        public static ConditionBasedVolumeStrategy Create() => new ConditionBasedVolumeStrategy();

        public ConditionBasedVolumeStrategy WithCondition(Func<bool> conditionToStop)
        {
            this.conditionToStop = conditionToStop;
            return this;
        }

        public bool HasMore => !this.conditionToStop();
        public bool TakeOneIfAny() => !this.conditionToStop();
    }
}
