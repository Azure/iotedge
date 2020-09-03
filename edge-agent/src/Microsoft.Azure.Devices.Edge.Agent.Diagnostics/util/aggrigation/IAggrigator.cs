// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggregation
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public interface IAggregator
    {
        public void PutValue(double value);

        public double GetAggregate();

        public IAggregator New();
    }
}
