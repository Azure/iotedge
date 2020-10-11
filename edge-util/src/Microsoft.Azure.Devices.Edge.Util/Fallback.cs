// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class Fallback
    {
        public static Task<ITry<bool>> ExecuteAsync(Func<Task> primary, Func<Task> secondary)
        {
            Preconditions.CheckNotNull(primary, nameof(primary));
            Preconditions.CheckNotNull(secondary, nameof(secondary));
            return ExecuteAsync(new[] { primary, secondary });
        }

        public static Task<ITry<T>> ExecuteAsync<T>(Func<Task<T>> primary, Func<Task<T>> secondary)
        {
            Preconditions.CheckNotNull(primary, nameof(primary));
            Preconditions.CheckNotNull(secondary, nameof(secondary));
            return ExecuteAsync(new[] { primary, secondary });
        }

        public static Task<ITry<bool>> ExecuteAsync(params Func<Task>[] options)
        {
            Preconditions.CheckNotNull(options, nameof(options));
            return ExecuteAsync(
                options.Select<Func<Task>, Func<Task<bool>>>(
                    o => (async () =>
                    {
                        await o();
                        return true;
                    })).ToArray());
        }

        public static async Task<ITry<T>> ExecuteAsync<T>(params Func<Task<T>>[] options)
        {
            Preconditions.CheckNotNull(options, nameof(options));

            var exceptions = new List<Exception>();
            foreach (Func<Task<T>> option in options)
            {
                try
                {
                    T result = await option();
                    return Try.Success(result);
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    exceptions.Add(ex);
                }
            }

            return Try.Failure<T>(new AggregateException(exceptions));
        }
    }
}
