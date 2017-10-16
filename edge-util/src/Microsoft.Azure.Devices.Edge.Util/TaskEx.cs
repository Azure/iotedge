// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TaskEx
    {
        public static Task Done { get; } = Task.FromResult(true);

        public static Task FromException(Exception exception) =>
            FromException<bool>(exception);

        public static Task<T> FromException<T>(Exception exception)
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.TrySetException(exception);
            return tcs.Task;
        }

        public static Task WhenCanceled(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        public async static Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> t1, Task<T2> t2)
        {
            var val1 = await t1;
            var val2 = await t2;
            return (val1, val2);
        }

        public async static Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> t1, Task<T2> t2, Task<T3> t3)
        {
            var val1 = await t1;
            var val2 = await t2;
            var val3 = await t3;
            return (val1, val2, val3);
        }

        public async static Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4)
        {
            var val1 = await t1;
            var val2 = await t2;
            var val3 = await t3;
            var val4 = await t4;
            return (val1, val2, val3, val4);
        }

        public async static Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5)
        {
            var val1 = await t1;
            var val2 = await t2;
            var val3 = await t3;
            var val4 = await t4;
            var val5 = await t5;
            return (val1, val2, val3, val4, val5);
        }

        public async static Task<(T1, T2, T3, T4, T5, T6)> WhenAll<T1, T2, T3, T4, T5, T6>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5, Task<T6> t6)
        {
            var val1 = await t1;
            var val2 = await t2;
            var val3 = await t3;
            var val4 = await t4;
            var val5 = await t5;
            var val6 = await t6;
            return (val1, val2, val3, val4, val5, val6);
        }

        public async static Task<(T1, T2, T3, T4, T5, T6, T7)> WhenAll<T1, T2, T3, T4, T5, T6, T7>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5, Task<T6> t6, Task<T7> t7)
        {
            var val1 = await t1;
            var val2 = await t2;
            var val3 = await t3;
            var val4 = await t4;
            var val5 = await t5;
            var val6 = await t6;
            var val7 = await t7;
            return (val1, val2, val3, val4, val5, val6, val7);
        }

        public async static Task<(T1, T2, T3, T4, T5, T6, T7, T8)> WhenAll<T1, T2, T3, T4, T5, T6, T7, T8>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5, Task<T6> t6, Task<T7> t7, Task<T8> t8)
        {
            var val1 = await t1;
            var val2 = await t2;
            var val3 = await t3;
            var val4 = await t4;
            var val5 = await t5;
            var val6 = await t6;
            var val7 = await t7;
            var val8 = await t8;
            return (val1, val2, val3, val4, val5, val6, val7, val8);
        }

        public async static Task<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> WhenAll<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5, Task<T6> t6, Task<T7> t7, Task<T8> t8, Task<T9> t9)
        {
            var val1 = await t1;
            var val2 = await t2;
            var val3 = await t3;
            var val4 = await t4;
            var val5 = await t5;
            var val6 = await t6;
            var val7 = await t7;
            var val8 = await t8;
            var val9 = await t9;
            return (val1, val2, val3, val4, val5, val6, val7, val8, val9);
        }
    }
}
