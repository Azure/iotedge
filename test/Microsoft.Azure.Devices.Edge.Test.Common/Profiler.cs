// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public class Profiler
    {
        public static Task Run(string startMessage, Func<Task> func, string endMessage = "") => Run(
            startMessage,
            async () => {
                await func();
                return true;
            },
            endMessage
        );

        public static async Task<T> Run<T>(string startMessage, Func<Task<T>> func, string endMessage = "")
        {
            Console.Write(startMessage + (string.IsNullOrEmpty(endMessage) ? "..." : "\n"));
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            T t = await func();

            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;
            Console.WriteLine(
                (string.IsNullOrEmpty(endMessage) ? "done" : endMessage) + $" [{ts}]");
            return t;
        }
    }
}
