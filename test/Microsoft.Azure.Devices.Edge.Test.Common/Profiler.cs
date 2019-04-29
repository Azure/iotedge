// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Serilog;

    public class Profiler
    {
        public static Task Run(string message, Func<Task> func) => Run(
            message,
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
            string elapsed = $"{((double)stopwatch.ElapsedMilliseconds)/1000,9:+0.000s}";
            Log.Information("[{Elapsed}] {Message}", elapsed, message);
            return t;
        }
    }
}
