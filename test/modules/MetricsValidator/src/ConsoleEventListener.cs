// Copyright (c) Microsoft. All rights reserved.
namespace System.Diagnostics.Tracing
{
    using System.Globalization;
    using System.Linq;

    public sealed class ConsoleEventListener : EventListener
    {
        private readonly string[] eventFilters;
        private readonly object @lock = new object();

        public ConsoleEventListener(string filter)
        {
            this.eventFilters = new string[1];
            this.eventFilters[0] = filter ?? throw new ArgumentNullException(nameof(filter));

            this.InitializeEventSources();
        }

        public ConsoleEventListener(string[] filters)
        {
            this.eventFilters = filters ?? throw new ArgumentNullException(nameof(filters));
            if (this.eventFilters.Length == 0)
                throw new ArgumentException("Filters cannot be empty", nameof(filters));

            foreach (string filter in this.eventFilters)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    throw new ArgumentNullException(nameof(filters));
                }
            }

            this.InitializeEventSources();
        }

        private void InitializeEventSources()
        {
            foreach (EventSource source in EventSource.GetSources())
            {
                this.EnableEvents(source, EventLevel.LogAlways);
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);
            this.EnableEvents(
                eventSource,
#if !NET451
                EventLevel.LogAlways,
                EventKeywords.All);
#else
                EventLevel.LogAlways);
#endif
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (this.eventFilters == null)
                return;

            lock (this.@lock)
            {
                if (this.eventFilters.Any(ef => eventData.EventSource.Name.StartsWith(ef, StringComparison.Ordinal)))
                {
                    string eventIdent;
#if NET451
                    // net451 doesn't have EventName, so we'll settle for EventId
                    eventIdent = eventData.EventId.ToString(CultureInfo.InvariantCulture);
#else
                    eventIdent = eventData.EventName;
#endif
                    string text = $"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)} [{eventData.EventSource.Name}-{eventIdent}]{(eventData.Payload != null ? $" ({string.Join(", ", eventData.Payload)})." : string.Empty)}";

                    ConsoleColor origForeground = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(text);
                    Debug.WriteLine(text);
                    Console.ForegroundColor = origForeground;
                }
            }
        }
    }
}