// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public enum MessageSource
    {
        Invalid,
        Telemetry,
        TwinChangeEvents,
        DeviceLifecycleEvents,
        DeviceJobLifecycleEvents
    }

    public static class MessageSourceStrings
    {
        public const string Invalid = "Invalid";
        public const string Telemetry = "Telemetry";
        public const string TwinChangeEvents = "twinChangeEvents";
        public const string DeviceLifecycleEvents = "deviceLifecycleEvents";
        public const string DeviceJobLifecycleEvents = "deviceJobLifecycleEvents";

        public static string ToStringEx(this MessageSource messageSource)
        {
            switch (messageSource)
            {
                case MessageSource.Invalid:
                    return Invalid;

                case MessageSource.Telemetry:
                    return Telemetry;

                case MessageSource.TwinChangeEvents:
                    return TwinChangeEvents;

                case MessageSource.DeviceLifecycleEvents:
                    return DeviceLifecycleEvents;

                case MessageSource.DeviceJobLifecycleEvents:
                    return DeviceJobLifecycleEvents;

                default:
                    throw new InvalidOperationException("MessageSource is not supported: " + messageSource.ToString());
            }
        }
    }
}
