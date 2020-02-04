// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Threading.Tasks;

    public static class TryFinally
    {
        public static async Task DoAsync(Func<Task> tryFunc, Func<Task> finallyFunc)
        {
            try
            {
                await tryFunc();
            }

            // According to C# reference docs for 'try-finally', the finally
            // block may or may not run for unhandled exceptions. The
            // workaround is to catch the exception here and rethrow,
            // guaranteeing that the finally block will run.
            // ReSharper disable once RedundantCatchClause
            catch
            {
                throw;
            }
            finally
            {
                await finallyFunc();
            }
        }

        public static Task DoAsync(Func<Task> tryFunc, Action finallyFunc) => DoAsync(
            tryFunc,
            () =>
            {
                finallyFunc();
                return Task.CompletedTask;
            });
    }
}
