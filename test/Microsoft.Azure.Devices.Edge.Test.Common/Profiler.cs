// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Serilog;

    public class Profiler
    {
        public static Task Run(Func<Task> func, string message, params object[] properties) => Run(
            async () =>
            {
                await func();
                return true;
            },
            message,
            properties);

        public static async Task<T> Run<T>(Func<Task<T>> func, string message, params object[] properties)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            T t = await func();

            stopwatch.Stop();
            var args = new object[] { $"{((double)stopwatch.ElapsedMilliseconds) / 1000,9:+0.000s}" };
            Log.Information($"[{{Elapsed}}] {message}", args.Concat(properties).ToArray());
            return t;
        }
    }
}
