// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Retrying
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public interface IBackoff
    {
        IEnumerable<TimeSpan> GetBackoff();
    }
}
