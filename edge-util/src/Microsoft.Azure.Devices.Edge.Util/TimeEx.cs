// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Globalization;

    public static class TimeEx
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixTimestamp(this DateTime value) =>
            (long)value.ToUniversalTime().Subtract(Epoch).TotalSeconds;

        public static long ToUnixMillis(this DateTime value) =>
            (long)value.ToUniversalTime().Subtract(Epoch).TotalMilliseconds;

        public static string ToLogString(this DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
    }
}
