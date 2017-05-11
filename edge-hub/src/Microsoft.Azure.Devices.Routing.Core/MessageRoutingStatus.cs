// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public enum MessageRoutingStatus
    {
        Success,
        Dropped,
        Invalid,
        Orphaned
    }

    public static class MessageStatusStrings
    {
        public const string Success = "Success";
        public const string Dropped = "Dropped";
        public const string Invalid = "Invalid";
        public const string Orphaned = "Orphaned";

        public static string ToStringEx(this MessageRoutingStatus messageStatus)
        {
            switch (messageStatus)
            {
                case MessageRoutingStatus.Dropped:
                    return Dropped;

                case MessageRoutingStatus.Invalid:
                    return Invalid;

                case MessageRoutingStatus.Orphaned:
                    return Orphaned;

                case MessageRoutingStatus.Success:
                    return Success;

                default:
                    throw new InvalidOperationException("Message Status is not supported: " + messageStatus.ToString());
            }
        }
    }
}
