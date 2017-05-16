// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using Microsoft.Extensions.Logging;
    using Serilog;

    public static class Logger
    {
        static readonly Lazy<ILoggerFactory> LoggerLazy = new Lazy<ILoggerFactory>(() => GetLoggerFactory(), true);

        public static ILoggerFactory Factory => LoggerLazy.Value;

        static ILoggerFactory GetLoggerFactory()
        {
            Serilog.Core.Logger loggerConfig = new LoggerConfiguration()
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
