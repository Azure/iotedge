// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggregation
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class Summer : IAggregator
    {
        double sum = 0;

        public IAggregator New()
        {
            return new Summer();
        }

        public void PutValue(double value)
        {
            this.sum += value;
        }

        public double GetAggregate()
        {
            return this.sum;
        }
    }

    public class Multiplier : IAggregator
    {
        double product = 1;

        public IAggregator New()
        {
            return new Multiplier();
        }

        public void PutValue(double value)
        {
            this.product *= value;
        }

        public double GetAggregate()
        {
            return this.product;
        }
    }

    public class Averager : IAggregator
    {
        double sum = 0;
        double count = 0;

        public IAggregator New()
        {
            return new Averager();
        }

        public void PutValue(double value)
        {
            this.sum += value;
            this.count++;
        }

        public double GetAggregate()
        {
            return this.sum / this.count;
        }
    }
}
