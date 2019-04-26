// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Serilog;

    public class Profiler
    {
        public static Task Run(string startMessage, Func<Task> func) => Run(
            startMessage,
            async () =>
            {
                await func();
                return true;
            });

        public static async Task<T> Run<T>(string message, Func<Task<T>> func)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            T t = await func();

            stopwatch.Stop();
            Log.Information("[+{Elapsed}] {Message}", stopwatch.Elapsed, message);
            return t;
        }
    }
}
