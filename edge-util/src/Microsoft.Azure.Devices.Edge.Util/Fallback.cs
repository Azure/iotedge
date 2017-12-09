// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading.Tasks;

    public static class Fallback
    {
        public static Task ExecuteAsync(Func<Task> primary, Func<Task> secondary) => ExecuteAsync(primary, secondary, null);
        public static Task<T> ExecuteAsync<T>(Func<Task<T>> primary, Func<Task<T>> secondary) => ExecuteAsync(primary, secondary, null);

        public static Task ExecuteAsync(
            Func<Task> primary,
            Func<Task> secondary,
            Action<Exception> onFailed)
        {
            return ExecuteAsync(
                async () => { await primary(); return true; },
                async () => { await secondary(); return true; },
                onFailed);
        }

        public static async Task<T> ExecuteAsync<T>(
            Func<Task<T>> primary,
            Func<Task<T>> secondary,
            Action<Exception> onFailed)
        {
            try
            {
                return await primary();
            }
            catch (Exception primaryEx)
            {
                onFailed?.Invoke(primaryEx);
                if (primaryEx.IsFatal())
                    throw;

                try
                {
                    return await secondary();
                }
                catch (Exception secondaryEx)
                {
                    onFailed?.Invoke(secondaryEx);
                    throw;
                }
            }
        }
    }
}
