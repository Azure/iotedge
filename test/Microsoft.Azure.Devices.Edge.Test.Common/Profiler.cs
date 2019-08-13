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
        readonly Stopwatch stopwatch = Stopwatch.StartNew();

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
            Profiler profiler = Start();
            try
            {
                T t = await func();
                profiler.Stop(message, properties);
                return t;
            }
            catch (Exception e)
            {
                Log.Error($"Encountered exception during task \"{message}\": {e.Message}", properties);
                throw;
            }
        }

        public static Profiler Start() => new Profiler();

        public void Stop(string message, params object[] properties)
        {
            if (!this.stopwatch.IsRunning)
            {
                throw new InvalidOperationException("Method 'Stop' called more than once on this instance of Profiler");
            }

            this.stopwatch.Stop();
            var args = new object[] { $"{((double)this.stopwatch.ElapsedMilliseconds) / 1000,9:+0.000s}" };
            Log.Information($"[{{Elapsed}}] {message}", args.Concat(properties).ToArray());
        }
    }
}
