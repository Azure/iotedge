// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    public class TelemetryMessageSource : BaseMessageSource
    {
        TelemetryMessageSource()
            : base("/messages/events")
        {
        }

        public static TelemetryMessageSource Instance { get; } = new TelemetryMessageSource();
    }
}