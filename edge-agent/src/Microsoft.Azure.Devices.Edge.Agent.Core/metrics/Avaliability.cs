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
        private DateTime? previousMeasure = DateTime.Now;

        public Avaliability(string name, string version)
        {
            Console.WriteLine($"make {name}");
            this.name = name;
            this.version = version;
        }

        public double avaliability { get { return uptime.TotalMilliseconds / totalTime.TotalMilliseconds; } }

        public void AddPoint(bool isUp)
        {
            Console.WriteLine($"{name}: {isUp} - {uptime} | {totalTime} = {avaliability}");
            /* if no previous measure, cannot compute duration. There must be 2 consecutive points to do so */
            if (previousMeasure == null)
            {
                previousMeasure = DateTime.Now;
                return;
            }

            TimeSpan duration = DateTime.Now - previousMeasure.Value;
            totalTime += duration;
            if (isUp)
            {
                uptime += duration;
            }
            previousMeasure = DateTime.Now;
        }

        public void NoPoint()
        {
            Console.WriteLine($"{name}: no point = {avaliability}");
            previousMeasure = null;
        }
    }
}
