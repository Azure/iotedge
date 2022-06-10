// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;
    using Serilog.Events;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    public static class MetricsUtil
    {
        internal static ILogger CreateLogger(string categoryName, LogEventLevel logEventLevel = LogEventLevel.Debug)
        {
            Preconditions.CheckNonWhiteSpace(categoryName, nameof(categoryName));

            var levelSwitch = new LoggingLevelSwitch(logEventLevel);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            return new LoggerFactory().AddSerilog().CreateLogger(categoryName);
        }
    }
}
