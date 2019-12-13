// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Retrying
{
    using System;
    using System.Collections.Generic;
    using System.Net.NetworkInformation;
    using System.Text;

    public class ExponentialBackoff : IBackoff
    {
        TimeSpan current;
        double multiplier;
        TimeSpan? max;

        public ExponentialBackoff(TimeSpan start, double multiplier, TimeSpan? max = null)
        {
            Preconditions.CheckArgument(start > TimeSpan.Zero, "Start must be > 0");
            Preconditions.CheckArgument(multiplier > 1, "Multiplier must be > 1");
            this.current = start;
            this.multiplier = multiplier;
            this.max = max;
        }

        public IEnumerable<TimeSpan> GetBackoff()
        {
            while (true)
            {
                yield return this.current;
                this.current = new TimeSpan((long)(this.current.Ticks * this.multiplier));

                if (this.max.HasValue && this.max < this.current)
                {
                    while (true)
                    {
                        yield return this.max.Value;
                    }
                }
            }
        }
    }
}
