// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Concurrency
{
    using System.Threading;

    public class AtomicLong
    {
        long underlying;

        public AtomicLong(long value)
        {
            this.underlying = value;
        }

        public AtomicLong()
            : this(0)
        {
        }

        public static implicit operator long(AtomicLong value) => value.Get();

        public long Get() => this.underlying;

        public long Set(long value) => Interlocked.Exchange(ref this.underlying, value);

        public long GetAndSet(long value) => Interlocked.Exchange(ref this.underlying, value);

        public long Increment() => Interlocked.Increment(ref this.underlying);

        public long CompareAndSet(long expected, long result)
        {
            return Interlocked.CompareExchange(ref this.underlying, result, expected);
        }
    }
}
