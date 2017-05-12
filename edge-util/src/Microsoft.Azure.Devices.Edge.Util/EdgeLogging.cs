// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;

    public static class EdgeLogging
    {
        static readonly Lazy<ILoggerFactory> LoggerLazy = new Lazy<ILoggerFactory>(() => GetLoggerFactory(), true);

        public static ILoggerFactory LoggerFactory => LoggerLazy.Value;

        static ILoggerFactory GetLoggerFactory()
        {
            Logger loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] - {Message}{NewLine}{Exception}"
                )
                .CreateLogger();

            ILoggerFactory factory = new LoggerFactory()
                .AddSerilog(loggerConfig);

            return factory;
        }
    }
}
