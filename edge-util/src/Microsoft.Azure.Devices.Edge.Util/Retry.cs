// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading.Tasks;

    public static class Retry
    {
        public static async Task<T> Do<T>(
            Func<Task<T>> func,
            Func<T, bool> isValidValue,
            Func<Exception, bool> continueOnException,
            TimeSpan retryInterval,
            int retryCount)
        {
            Preconditions.CheckNotNull(func, nameof(func));
            
            T result = default(T);
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    result = await func();
                    if (isValidValue == null || isValidValue(result))
                    {
                        return result;
                    }

                    if (i < retryCount - 1)
                    {
                        await Task.Delay(retryInterval);
                    }
                }
                catch (Exception e)
                {
                    if (i == retryCount - 1 || (continueOnException != null && !continueOnException(e)))
                    {
                        throw;
                    }
                }
            }
            return result;
        }        
    }
}
