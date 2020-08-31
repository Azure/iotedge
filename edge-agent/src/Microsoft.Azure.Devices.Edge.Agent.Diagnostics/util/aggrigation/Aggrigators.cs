// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggrigation
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class Summer : IAggrigator
    {
        double sum = 0;

        public IAggrigator New()
        {
            return new Summer();
        }

        public void PutValue(double value)
        {
            this.sum += value;
        }

        public double GetAggrigate()
        {
            return this.sum;
        }
    }

    public class Multiplier : IAggrigator
    {
        double product = 1;

        public IAggrigator New()
        {
            return new Multiplier();
        }

        public void PutValue(double value)
        {
            this.product *= value;
        }

        public double GetAggrigate()
        {
            return this.product;
        }
    }

    public class Averager : IAggrigator
    {
        double sum = 0;
        double count = 0;

        public IAggrigator New()
        {
            return new Averager();
        }

        public void PutValue(double value)
        {
            this.sum += value;
            this.count++;
        }

        public double GetAggrigate()
        {
            return this.sum / this.count;
        }
    }
}
