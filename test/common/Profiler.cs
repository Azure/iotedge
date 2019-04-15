// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace common
{
    public class Profiler
    {
        public static async Task Run(string startMessage, Func<Task> func)
        {
            Console.Write($"{startMessage}...");
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            await func();

            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;
            Console.WriteLine($"done. [{ts}]");
        }
    }
}