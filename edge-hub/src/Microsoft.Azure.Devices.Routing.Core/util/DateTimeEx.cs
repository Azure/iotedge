// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Util
{
    using System;

    public static class DateTimeEx
    {
        /// <summary>
        /// Adds timespan to the source datetime, but returns DateTime.MaxValue
        /// (or DateTime.MinValue) instead of throwing ArgumentOutOfRangeException.
        /// </summary>
        /// <param name="src">Source DateTime</param>
        /// <param name="timespan">TimeSpan to add to DateTime</param>
        /// <returns>Updated DateTime, clamped to DateTime.MinValue or DateTime.MaxValue</returns>
        public static DateTime SafeAdd(this DateTime src, TimeSpan timespan)
        {
            if (timespan.Ticks > 0)
            {
                TimeSpan toAdd = DateTime.MaxValue - src;
                return timespan.Ticks > toAdd.Ticks ? DateTime.MaxValue : src + timespan;
            }
            else
            {
                TimeSpan toSubtract = src - DateTime.MinValue;
                TimeSpan absTimeSpan = timespan == TimeSpan.MinValue ? TimeSpan.MaxValue : -timespan;
                return absTimeSpan.Ticks > toSubtract.Ticks ? DateTime.MinValue : src + timespan;
            }
        }
    }
}