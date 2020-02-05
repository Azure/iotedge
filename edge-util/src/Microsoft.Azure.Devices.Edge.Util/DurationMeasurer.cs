// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;

    public static class DurationMeasurer
    {
        public static IDisposable MeasureDuration(Action<TimeSpan> onCompleation)
        {
            return new DurationSetter(onCompleation);
        }

        class DurationSetter : IDisposable
        {
            Stopwatch timer = Stopwatch.StartNew();
            Action<TimeSpan> setDuration;

            internal DurationSetter(Action<TimeSpan> setDuration)
            {
                this.setDuration = setDuration;
            }

            public void Dispose()
            {
                this.timer.Stop();
                this.setDuration(this.timer.Elapsed);
            }
        }
    }
}
