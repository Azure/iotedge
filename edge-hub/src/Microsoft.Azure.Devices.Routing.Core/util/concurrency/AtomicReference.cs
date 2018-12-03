// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Util.Concurrency
{
    using System.Threading;

    public class AtomicReference<T> where T : class
    {
        T val;

        public AtomicReference(T value)
        {
            this.val = value;
        }

        public bool CompareAndSet(T expect, T update)
        {
            return Interlocked.CompareExchange(ref this.val, update, expect) == expect;
        }

        public T GetAndSet(T t)
        {
            return Interlocked.Exchange(ref this.val, t);
        }

        public T Get()
        {
            return this.val;
        }

        public static implicit operator T(AtomicReference<T> reference)
        {
            return reference.Get();
        }
    }
}
