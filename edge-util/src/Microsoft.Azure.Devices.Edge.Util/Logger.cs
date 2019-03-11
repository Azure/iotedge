// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;
    using Serilog.Events;

    public static class Logger
    {
        public const string RuntimeLogLevelEnvKey = "RuntimeLogLevel";

        static readonly Dictionary<string, LogEventLevel> LogLevelDictionary = new Dictionary<string, LogEventLevel>(StringComparer.OrdinalIgnoreCase)
        {
            { "verbose", LogEventLevel.Verbose },
            { "debug", LogEventLevel.Debug },
            { "info", LogEventLevel.Information },
            { "information", LogEventLevel.Information },
            { "warning", LogEventLevel.Warning },
            { "error", LogEventLevel.Error },
            { "fatal", LogEventLevel.Fatal }
        };

        static readonly Lazy<ILoggerFactory> LoggerLazy = new Lazy<ILoggerFactory>(() => GetLoggerFactory(), true);
        static LogEventLevel logLevel = LogEventLevel.Information;

        public static ILoggerFactory Factory => LoggerLazy.Value;

        public static void SetLogLevel(string level)
        {
            Preconditions.CheckNonWhiteSpace(level, nameof(level));
            logLevel = LogLevelDictionary.GetOrElse(level.ToLower(), LogEventLevel.Information);
        }

        public static LogEventLevel GetLogLevel() => logLevel;

        static ILoggerFactory GetLoggerFactory()
        {
            var levelSwitch = new LoggingLevelSwitch();
            levelSwitch.MinimumLevel = logLevel;
            Serilog.Core.Logger loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .Enrich.FromLogContext()
                .Enrich.With(SeverityEnricher.Instance)
                .WriteTo.Console(
                    outputTemplate: "<{Severity}> {Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] - {Message}{NewLine}{Exception}").CreateLogger();

            if (levelSwitch.MinimumLevel <= LogEventLevel.Debug)
            {
                // Overwrite with richer content if less then debug
                loggerConfig = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(levelSwitch)
                    .Enrich.FromLogContext()
                    .Enrich.With(SeverityEnricher.Instance)
                    .WriteTo.Console(
                        outputTemplate: "<{Severity}> {Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext:1}] - {Message}{NewLine}{Exception}").CreateLogger();
            }

            ILoggerFactory factory = new LoggerFactory()
                .AddSerilog(loggerConfig);

            return factory;
        }

        // This maps the Edge log level to the severity level based on Syslog severity levels.
        // https://en.wikipedia.org/wiki/Syslog#Severity_level
        // This allows tools to parse the severity level from the log text and use it to enhance the log
        // For example errors can show up as red
        class SeverityEnricher : ILogEventEnricher
        {
            static readonly IDictionary<LogEventLevel, int> LogLevelSeverityMap = new Dictionary<LogEventLevel, int>
            {
                [LogEventLevel.Fatal] = 0,
                [LogEventLevel.Error] = 3,
                [LogEventLevel.Warning] = 4,
                [LogEventLevel.Information] = 6,
                [LogEventLevel.Debug] = 7,
                [LogEventLevel.Verbose] = 7
            };

            SeverityEnricher()
            {
            }

            public static SeverityEnricher Instance => new SeverityEnricher();

            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) =>
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                    "Severity", LogLevelSeverityMap[logEvent.Level]));
        }
    }
}
