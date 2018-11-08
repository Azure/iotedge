// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

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
