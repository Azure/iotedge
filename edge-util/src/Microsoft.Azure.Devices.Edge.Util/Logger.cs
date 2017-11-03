// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Events;
    using Serilog.Core;

    public static class Logger
    {
        public const string RuntimeLogLevelEnvKey = "RuntimeLogLevel";

        static Dictionary<string, LogEventLevel> LogLevelDictionary = new Dictionary<string, LogEventLevel>()
        {
            {"verbose", LogEventLevel.Verbose},
            {"debug", LogEventLevel.Debug},
            {"info", LogEventLevel.Information},
            {"warning", LogEventLevel.Warning},
            {"error", LogEventLevel.Error},
            {"fatal", LogEventLevel.Fatal}
        };

        static LogEventLevel logLevel = LogEventLevel.Information;
        public static void SetLogLevel(string level)
        {
            Preconditions.CheckNonWhiteSpace(level, nameof(level));
            logLevel = LogLevelDictionary.GetOrElse(level.ToLower(), LogEventLevel.Information);
        }

        public static LogEventLevel GetLogLevel()
        {
            return logLevel;
        }

        static readonly Lazy<ILoggerFactory> LoggerLazy = new Lazy<ILoggerFactory>(() => GetLoggerFactory(), true);

        public static ILoggerFactory Factory => LoggerLazy.Value;

        static ILoggerFactory GetLoggerFactory()
        {
            var levelSwitch = new LoggingLevelSwitch();
            levelSwitch.MinimumLevel = logLevel;
            Serilog.Core.Logger loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
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
