using Microsoft.Azure.Devices.Edge.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    public class Avaliability
    {
        public string name;
        public string version;
        private TimeSpan totalTime = TimeSpan.Zero;
        private TimeSpan uptime = TimeSpan.Zero;
        private DateTime? previousMeasure = null;
        private readonly ISystemTime time;

        public Avaliability(string name, string version, ISystemTime time)
        {
            this.name = name;
            this.version = version;

            this.time = time;
            previousMeasure = time.UtcNow;
        }

        public Avaliability(AvaliabilityRaw raw, ISystemTime time)
        {
            this.name = raw.name;
            this.version = raw.version;
            this.uptime = raw.uptime;
            this.totalTime = raw.totalTime;

            this.time = time;
        }

        public double avaliability { get { return uptime.TotalMilliseconds / totalTime.TotalMilliseconds; } }

        public void AddPoint(bool isUp)
        {
            /* if no previous measure, cannot compute duration. There must be 2 consecutive points to do so */
            if (previousMeasure == null)
            {
                previousMeasure = time.UtcNow;
                return;
            }

            TimeSpan duration = time.UtcNow - previousMeasure.Value;
            totalTime += duration;
            if (isUp)
            {
                uptime += duration;
            }
            previousMeasure = time.UtcNow;
        }

        public void NoPoint()
        {
            previousMeasure = null;
        }

        public AvaliabilityRaw ToRaw()
        {
            return new AvaliabilityRaw
            {
                name = name,
                version = version,
                uptime = uptime,
                totalTime = totalTime
            };
        }
    }

    public struct AvaliabilityRaw
    {
        public string name;
        public string version;
        public TimeSpan totalTime;
        public TimeSpan uptime;
    }
}
