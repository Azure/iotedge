// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading.Tasks;

    public static class Fallback
    {
        public static Task ExecuteAsync(Func<Task> primary, Func<Task> secondary)
        {
            return ExecuteAsync(
                async () => { await primary(); return true; },
                async () => { await secondary(); return true; });
        }

        public static async Task<T> ExecuteAsync<T>(Func<Task<T>> primary, Func<Task<T>> secondary)
        {
            try
            {
                return await primary();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                return await secondary();
            }
        }
    }
}
