// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggrigation
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public interface IAggrigator
    {
        public void PutValue(double value);

        public double GetAggrigate();

        public IAggrigator New();
    }
}
