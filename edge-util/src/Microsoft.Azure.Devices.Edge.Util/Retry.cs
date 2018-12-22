// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Retry
    {
        static async Task<Option<T>> DoOnce<T>(
            Func<Task<T>> func,
            Func<T, bool> isValidValue,
            Func<Exception, bool> continueOnException)
        {
            try
            {
                T result = await func();
                if (isValidValue == null || isValidValue(result))
                {
                    return Option.Some(result);
                }
            }
            catch (Exception e)
            {
                if (continueOnException == null || !continueOnException(e))
                {
                    throw;
                }
            }
            return Option.None<T>();
        }

        public static async Task<T> Do<T>(
            Func<Task<T>> func,
            Func<T, bool> isValidValue,
            Func<Exception, bool> continueOnException,
            TimeSpan retryInterval,
            CancellationToken token)
        {
            Preconditions.CheckNotNull(func, nameof(func));

            while (!token.IsCancellationRequested)
            {
                Option<T> result = await DoOnce(func, isValidValue, continueOnException);
                if (result.HasValue)
                {
                    return result.OrDefault();
                }
                else
                {
                    await Task.Delay(retryInterval, token);
                }
            }
            throw new TaskCanceledException();
        }
    }
}
