// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    class Program
    {
        public static int Main(string[] args)
        {
            Logger loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] - {Message}{NewLine}{Exception}"
                )
                .CreateLogger();

            ILoggerFactory factory = new LoggerFactory()
                .AddSerilog(loggerConfig);
            ILogger logger = factory.CreateLogger<Program>();

            logger.LogInformation("Starting local IoT Hub.");

            for (int i = 0; i < 1000; i++)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                logger.LogInformation("Working... [{i}]", i);
            }
            return 0;
        }
    }
}