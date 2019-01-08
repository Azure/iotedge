// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    public static class TimeEx
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixTimestamp(this DateTime value) =>
            (long)value.ToUniversalTime().Subtract(Epoch).TotalSeconds;

        public static long ToUnixMillis(this DateTime value) =>
            (long)value.ToUniversalTime().Subtract(Epoch).TotalMilliseconds;
    }
}