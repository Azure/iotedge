// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Util.Concurrency
{
    using System.Threading;

    public class AtomicReference<T>
        where T : class
    {
        T val;

        public AtomicReference(T value)
        {
            this.val = value;
        }

        public static implicit operator T(AtomicReference<T> reference)
        {
            return reference.Get();
        }

        public bool CompareAndSet(T expect, T update)
        {
            return Interlocked.CompareExchange(ref this.val, update, expect) == expect;
        }

        public T Get()
        {
            return this.val;
        }

        public T GetAndSet(T t)
        {
            return Interlocked.Exchange(ref this.val, t);
        }
    }
}
