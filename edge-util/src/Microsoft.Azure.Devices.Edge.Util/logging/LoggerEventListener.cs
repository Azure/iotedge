// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Logging
{
    using System.Diagnostics.Tracing;
    using System.Linq;
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

        private static readonly string[] s_eventFilter = new string[] { "DotNetty-Default", "Microsoft-Azure-Devices", "Microsoft-Azure-Devices-Provisioning-Transport-Mqtt", "Azure-Core", "Azure-Identity" };

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (s_eventFilter.Any(filter => eventSource.Name.StartsWith(filter, StringComparison.OrdinalIgnoreCase)))
            {
                base.OnEventSourceCreated(eventSource);
                EnableEvents(
                    eventSource,
                    EventLevel.LogAlways,
                     EventKeywords.All
                );
            }
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
