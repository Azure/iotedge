// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Prints SDK events to Console output - the log level is set to INFORMATION
    /// </summary>
    public sealed class LeafDeviceSdkLogger : EventListener
    {
        private readonly string[] eventFilters;
        private readonly ILogger logger;
        private readonly object @lock = new object();

        public LeafDeviceSdkLogger(string filter, ILogger logger)
            : this(new string[] { filter }, logger)
        {
        }

        public LeafDeviceSdkLogger(string[] filters, ILogger logger)
        {
            this.eventFilters = filters ?? throw new ArgumentNullException(nameof(filters));
            if (this.eventFilters.Length == 0)
            {
                throw new ArgumentException("Filters cannot be empty", nameof(filters));
            }

            foreach (string filter in this.eventFilters)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    throw new ArgumentNullException(nameof(filters));
                }
            }

            this.logger = logger;

            foreach (EventSource source in EventSource.GetSources())
            {
                this.EnableEvents(source, EventLevel.LogAlways);
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);
            this.EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (this.eventFilters == null)
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.eventFilters.Any(ef => eventData.EventSource.Name.StartsWith(ef, StringComparison.Ordinal)))
                {
                    string text = $"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)} [SDK] [{eventData.EventSource.Name}-{eventData.EventName}]{(eventData.Payload != null ? $" ({string.Join(", ", eventData.Payload)})." : string.Empty)}";
                    this.logger.LogInformation(text);
                }
            }
        }
    }
}
