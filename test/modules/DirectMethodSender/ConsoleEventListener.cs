// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Linq;

    public sealed class ConsoleEventListener : EventListener
    {
        // Configure this value to filter all the necessary events when OnEventSourceCreated is called.
        // The EventListener base class constructor creates an event listener in which all events are disabled by default.
        // EventListener constructor also causes the OnEventSourceCreated callback to fire.
        // Since our ConsoleEventListener uses the OnEventSourceCreated callback to enable events, the event filter needs to be
        // initialized before OnEventSourceCreated is called. For this reason we cannot use ConsoleEventListener constructor
        // to initialize the event filter (base class constructors are called before derived class constructors).
        // The OnEventSourceCreated will be triggered sooner than the filter is initialized in the ConsoleEventListener constructor.
        // As a result we will need to define the event filter list as a static variable.
        // Link to EventListener sourcecode: https://github.com/dotnet/runtime/blob/6696065ab0f517f5a9e5f55c559df0010a816dbe/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/EventSource.cs#L4009-L4018
        private static readonly string[] EventFilter = new string[] { "Microsoft-Azure-Devices", "Azure-Core", "Azure-Identity" };

        private readonly object eventlock = new object();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (EventFilter.Any(filter => eventSource.Name.StartsWith(filter, StringComparison.OrdinalIgnoreCase)))
            {
                base.OnEventSourceCreated(eventSource);
#if !NET451
                this.EnableEvents(
                    eventSource,
                    EventLevel.LogAlways,
                    EventKeywords.All);
#else
                this.EnableEvents(
                    eventSource,
                    EventLevel.LogAlways);
#endif
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            lock (this.eventlock)
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
