// Copyright (c) Microsoft. All rights reserved.
namespace NumberLogger
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging.Abstractions;

    class Program
    {
        public static void Main()
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), NullLogger.Instance);

            string countString = Environment.GetEnvironmentVariable("Count");
            int parsedCount = int.Parse(countString);

            for (int i = 0; i < parsedCount; i++)
            {
                Console.WriteLine(i);
            }

            cts.Token.WhenCanceled().Wait();

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
        }
    }
}
