// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Availability
    {
        private readonly ISystemTime time;

        public string Name { get; private set; }
        public string Version { get; private set; }
        public TimeSpan RunningTime { get; private set; } = TimeSpan.Zero;
        public TimeSpan ExpectedTime { get; private set; } = TimeSpan.Zero;

        private DateTime? previousMeasure = null;

        public Availability(string name, string version, ISystemTime time)
        {
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.time = Preconditions.CheckNotNull(time, nameof(time));
            this.previousMeasure = time.UtcNow;
        }

        public Availability(AvailabilityRaw raw, ISystemTime time)
        {
            this.Name = Preconditions.CheckNotNull(raw.Name);
            this.Version = Preconditions.CheckNotNull(raw.Version);
            this.RunningTime = Preconditions.CheckNotNull(raw.Uptime);
            this.ExpectedTime = Preconditions.CheckNotNull(raw.TotalTime);

            this.time = Preconditions.CheckNotNull(time, nameof(time));
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

        public AvailabilityRaw ToRaw()
        {
            return new AvailabilityRaw
            {
                Name = this.Name,
                Version = this.Version,
                Uptime = this.RunningTime,
                TotalTime = this.ExpectedTime
            };
        }
    }

    public struct AvailabilityRaw
    {
        public string Name;
        public string Version;
        public TimeSpan TotalTime;
        public TimeSpan Uptime;
    }
}
