// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Linq;

namespace System.Diagnostics.Tracing
{
    public sealed class ConsoleEventListener : EventListener
    {
        private readonly string[] _eventFilters;
        private readonly object _lock = new object();

        public ConsoleEventListener(string filter)
        {
            _eventFilters = new string[1];
            _eventFilters[0] = filter ?? throw new ArgumentNullException(nameof(filter));

            InitializeEventSources();
        }

        public ConsoleEventListener(string[] filters)
        {
            _eventFilters = filters ?? throw new ArgumentNullException(nameof(filters));
            if (_eventFilters.Length == 0) throw new ArgumentException("Filters cannot be empty", nameof(filters));

            foreach (string filter in _eventFilters)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    throw new ArgumentNullException(nameof(filters));
                }
            }

            InitializeEventSources();
        }

        private void InitializeEventSources()
        {
            foreach (EventSource source in EventSource.GetSources())
            {
                EnableEvents(source, EventLevel.LogAlways);
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);
            EnableEvents(
                eventSource,
                EventLevel.LogAlways
#if !NET451
                , EventKeywords.All
#endif
                );
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (_eventFilters == null) return;

            lock (_lock)
            {
                if (_eventFilters.Any(ef => eventData.EventSource.Name.StartsWith(ef, StringComparison.Ordinal)))
                {
                    string eventIdent;
#if NET451
                    // net451 doesn't have EventName, so we'll settle for EventId
                    eventIdent = eventData.EventId.ToString(CultureInfo.InvariantCulture);
#else
                    eventIdent = eventData.EventName;
#endif
                    string text = $"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)} [{eventData.EventSource.Name}-{eventIdent}]{(eventData.Payload != null ? $" ({string.Join(", ", eventData.Payload)})." : "")}";

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
