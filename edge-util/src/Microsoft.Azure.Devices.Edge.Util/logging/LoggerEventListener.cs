// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Logging
{
    using System.Diagnostics.Tracing;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Adapts an EventSource to a ILogger
    /// </summary>
    public class LoggerEventListener : EventListener
    {
        readonly ILogger logger;

        public LoggerEventListener(ILogger logger)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            this.logger.Log(GetLogLevel(eventData.Level), eventData.EventId, eventData, null, (ev, ex) => Formatter(ev));
        }

        static string Formatter(EventWrittenEventArgs args) =>
             args?.Payload != null
                ? string.Join(", ", args.Payload.Select(e => e.ToString()))
                : "<null>";

        static LogLevel GetLogLevel(EventLevel level)
        {
            switch (level)
            {
                case EventLevel.Critical:
                    return LogLevel.Critical;
                case EventLevel.Error:
                    return LogLevel.Error;
                case EventLevel.Warning:
                    return LogLevel.Warning;
                case EventLevel.Informational:
                    return LogLevel.Information;
                case EventLevel.Verbose:
                    return LogLevel.Debug;
                default:
                    return LogLevel.None;
            }
        }
    }
}