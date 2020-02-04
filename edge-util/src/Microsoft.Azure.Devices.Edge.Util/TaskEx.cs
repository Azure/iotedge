// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Linq;
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

        public static async Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> t1, Task<T2> t2)
        {
            T1 val1 = await t1;
            T2 val2 = await t2;
            return (val1, val2);
        }

        public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> t1, Task<T2> t2, Task<T3> t3)
        {
            T1 val1 = await t1;
            T2 val2 = await t2;
            T3 val3 = await t3;
            return (val1, val2, val3);
        }

        public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4)
        {
            T1 val1 = await t1;
            T2 val2 = await t2;
            T3 val3 = await t3;
            T4 val4 = await t4;
            return (val1, val2, val3, val4);
        }

        public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5)
        {
            T1 val1 = await t1;
            T2 val2 = await t2;
            T3 val3 = await t3;
            T4 val4 = await t4;
            T5 val5 = await t5;
            return (val1, val2, val3, val4, val5);
        }

        public static async Task<(T1, T2, T3, T4, T5, T6)> WhenAll<T1, T2, T3, T4, T5, T6>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5, Task<T6> t6)
        {
            T1 val1 = await t1;
            T2 val2 = await t2;
            T3 val3 = await t3;
            T4 val4 = await t4;
            T5 val5 = await t5;
            T6 val6 = await t6;
            return (val1, val2, val3, val4, val5, val6);
        }

        public static async Task<(T1, T2, T3, T4, T5, T6, T7)> WhenAll<T1, T2, T3, T4, T5, T6, T7>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5, Task<T6> t6, Task<T7> t7)
        {
            T1 val1 = await t1;
            T2 val2 = await t2;
            T3 val3 = await t3;
            T4 val4 = await t4;
            T5 val5 = await t5;
            T6 val6 = await t6;
            T7 val7 = await t7;
            return (val1, val2, val3, val4, val5, val6, val7);
        }

        public static async Task<(T1, T2, T3, T4, T5, T6, T7, T8)> WhenAll<T1, T2, T3, T4, T5, T6, T7, T8>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5, Task<T6> t6, Task<T7> t7, Task<T8> t8)
        {
            T1 val1 = await t1;
            T2 val2 = await t2;
            T3 val3 = await t3;
            T4 val4 = await t4;
            T5 val5 = await t5;
            T6 val6 = await t6;
            T7 val7 = await t7;
            T8 val8 = await t8;
            return (val1, val2, val3, val4, val5, val6, val7, val8);
        }

        public static async Task<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> WhenAll<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5, Task<T6> t6, Task<T7> t7, Task<T8> t8, Task<T9> t9)
        {
            T1 val1 = await t1;
            T2 val2 = await t2;
            T3 val3 = await t3;
            T4 val4 = await t4;
            T5 val5 = await t5;
            T6 val6 = await t6;
            T7 val7 = await t7;
            T8 val8 = await t8;
            T9 val9 = await t9;
            return (val1, val2, val3, val4, val5, val6, val7, val8, val9);
        }

        public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                Task timerTask = Task.Delay(timeout, cts.Token);
                Task completedTask = await Task.WhenAny(task, timerTask);
                if (completedTask == timerTask)
                {
                    throw new TimeoutException("Operation timed out");
                }

                cts.Cancel();
                return await task;
            }
        }

        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                Task timerTask = Task.Delay(timeout, cts.Token);
                Task completedTask = await Task.WhenAny(task, timerTask);
                if (completedTask == timerTask)
                {
                    throw new TimeoutException("Operation timed out");
                }

                cts.Cancel();
                await task;
            }
        }

        public static Task TimeoutAfter(this Func<CancellationToken, Task> operation, CancellationToken cancellationToken, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    return operation(CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken).Token)
                        .TimeoutAfter(timeout);
                }
                catch (TimeoutException)
                {
                    cts.Cancel();
                    throw;
                }
            }
        }

        public static Task<T> TimeoutAfter<T>(this Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    return operation(CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken).Token)
                        .TimeoutAfter(timeout);
                }
                catch (TimeoutException)
                {
                    cts.Cancel();
                    throw;
                }
            }
        }

        public static Task<T> ExecuteUntilCancelled<T>(this Func<T> operation, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(operation, nameof(operation));
            Task<T> task = Task.Run(operation, cancellationToken);
            return task.ExecuteUntilCancelled(cancellationToken);
        }

        public static Task ExecuteUntilCancelled(this Action operation, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(operation, nameof(operation));
            Task task = Task.Run(operation, cancellationToken);
            return task.ExecuteUntilCancelled(cancellationToken);
        }

        public static IAsyncResult ToAsyncResult(this Task task, AsyncCallback callback, object state)
        {
            if (task.AsyncState == state)
            {
                if (callback != null)
                {
                    task.ContinueWith(
                        (t, st) => ((AsyncCallback)state)(t),
                        callback,
                        TaskContinuationOptions.ExecuteSynchronously);
                }

                return task;
            }

            var tcs = new TaskCompletionSource<object>(state);
            task.ContinueWith(
                t =>
                {
                    switch (t.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            tcs.TrySetResult(null);
                            break;
                        case TaskStatus.Canceled:
                            tcs.TrySetCanceled();
                            break;
                        case TaskStatus.Faulted:
                            if (t.Exception != null)
                                tcs.TrySetException(t.Exception.InnerExceptions);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    callback?.Invoke(tcs.Task);
                },
                TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
        }

        public static void EndAsyncResult(IAsyncResult asyncResult)
        {
            if (!(asyncResult is Task task))
            {
                throw new ArgumentException("IAsyncResult should be of type Task");
            }

            try
            {
                task.Wait();
            }
            catch (AggregateException ae)
            {
                throw ae.GetBaseException();
            }
        }

        public static Task<Option<T>> MayThrow<T>(this Task<T> source, params Type[] allowedExceptions)
        {
            return MayThrow(source, _ => Option.None<T>(), allowedExceptions);
        }

        public static async Task<Option<T>> MayThrow<T>(this Task<T> source, Func<Exception, Option<T>> alternativeMaker, params Type[] allowedExceptions)
        {
            try
            {
                var result = await source;
                return Option.Some(result);
            }
            catch (Exception e) when (allowedExceptions.Contains(e.GetType()))
            {
                return alternativeMaker(e);
            }
        }

        static async Task<T> ExecuteUntilCancelled<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();
            cancellationToken.Register(
                () => { tcs.SetException(new TaskCanceledException(task)); });
            Task<T> completedTask = await Task.WhenAny(task, tcs.Task);
            return await completedTask;
        }

        static async Task ExecuteUntilCancelled(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<int>();
            cancellationToken.Register(
                () => { tcs.TrySetCanceled(); });
            Task completedTask = await Task.WhenAny(task, tcs.Task);
            //// Await here to bubble up any exceptions
            await completedTask;
        }
    }
}
