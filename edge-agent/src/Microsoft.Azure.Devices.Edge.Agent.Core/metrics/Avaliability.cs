// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Availability
    {
        readonly ISystemTime time;

        public string Name { get; private set; }
        public TimeSpan RunningTime { get; private set; } = TimeSpan.Zero;
        public TimeSpan ExpectedTime { get; private set; } = TimeSpan.Zero;

        DateTime? previousMeasure;

        public Availability(string name, ISystemTime time)
        {
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.time = Preconditions.CheckNotNull(time, nameof(time));
            this.previousMeasure = time.UtcNow;
        }

        // This is used to create edgeAgent's own avaliability, since it can't track its own downtime.
        public Availability(string name, TimeSpan downtime, ISystemTime time)
        {
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.time = Preconditions.CheckNotNull(time, nameof(time));
            this.previousMeasure = time.UtcNow;

            this.ExpectedTime = downtime;
        }

        public void AddPoint(bool isUp)
        {
            DateTime currentTime = this.time.UtcNow;
            /* if no previous measure, cannot compute duration. There must be 2 consecutive points to do so */
            if (this.previousMeasure == null)
            {
                this.previousMeasure = currentTime;
                return;
            }

            TimeSpan duration = currentTime - this.previousMeasure.Value;
            this.ExpectedTime += duration;
            if (isUp)
            {
                this.RunningTime += duration;
            }

            this.previousMeasure = currentTime;
        }

        public void NoPoint()
        {
            this.previousMeasure = null;
        }
    }
}
