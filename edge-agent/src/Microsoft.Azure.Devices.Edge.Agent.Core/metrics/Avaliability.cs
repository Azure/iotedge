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
            this.Name = name;
            this.Version = version;
            this.time = time;
            this.previousMeasure = time.UtcNow;
        }

        public Availability(AvailabilityRaw raw, ISystemTime time)
        {
            this.Name = raw.Name;
            this.Version = raw.Version;
            this.Uptime = raw.Uptime;
            this.TotalTime = raw.TotalTime;

            this.time = time;
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
            /* if no previous measure, cannot compute duration. There must be 2 consecutive points to do so */
            if (this.previousMeasure == null)
            {
                this.previousMeasure = this.time.UtcNow;
                return;
            }

            TimeSpan duration = this.time.UtcNow - this.previousMeasure.Value;
            this.TotalTime += duration;
            if (isUp)
            {
                this.Uptime += duration;
            }

            this.previousMeasure = this.time.UtcNow;
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
