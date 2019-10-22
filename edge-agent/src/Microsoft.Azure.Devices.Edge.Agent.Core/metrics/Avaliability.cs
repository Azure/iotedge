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

        public string Name;
        public string Version;
        public TimeSpan TotalTime = TimeSpan.Zero;
        public TimeSpan Uptime = TimeSpan.Zero;

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
            this.Uptime = Preconditions.CheckNotNull(raw.Uptime);
            this.TotalTime = Preconditions.CheckNotNull(raw.TotalTime);

            this.time = Preconditions.CheckNotNull(time, nameof(time));
        }

        public double AvailabilityRatio
        {
            get
            {
                if (this.TotalTime == TimeSpan.Zero)
                {
                    return 0;
                }

                return this.Uptime.TotalMilliseconds / this.TotalTime.TotalMilliseconds;
            }
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
            this.TotalTime += duration;
            if (isUp)
            {
                this.Uptime += duration;
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
                Uptime = this.Uptime,
                TotalTime = this.TotalTime
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
