// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    class Program
    {
        public static int Main(string[] args)
        {
            ILoggerFactory factory = new LoggerFactory()
                .AddConsole();
            ILogger logger = factory.CreateLogger<Program>();

            logger.LogInformation("Starting module management agent.");

            for (int i = 0; i < 1000; i++)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                logger.LogInformation("Working... [{i}]", i);
            }
            return 0;
        }
    }
}