// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace common
{
    public class Profiler
    {
        public static Task Run(string startMessage, Func<Task> func) => Run(
            startMessage,
            async () => {
                await func();
                return true;
            }
        );

        public static async Task<T> Run<T>(string startMessage, Func<Task<T>> func)
        {
            Console.Write($"{startMessage}...");
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            T t = await func();

            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;
            Console.WriteLine($"done. [{ts}]");
            return t;
        }
    }
}