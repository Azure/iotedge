// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    public static class TimeSpanEx
    {
        public static string Humanize(this TimeSpan ts)
        {
            string str;
            if (ts.TotalSeconds > 60.0)
            {
                str = string.Format("{0:D2}m:{1:D2}s", ts.Minutes, ts.Seconds);
            }
            else
            {
                str = string.Format("{0:D2}s", ts.Seconds);
            }

            return str;
        }
    }
}
