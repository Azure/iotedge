// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using System;

    public class CheckpointData
    {
        public CheckpointData(long offset) : this(offset, Option.None<DateTime>(), Option.None<DateTime>())
        {
        }

        public CheckpointData(long offset, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince)
        {
            this.LastFailedRevivalTime = lastFailedRevivalTime.GetOrElse(Checkpointer.DateTimeMinValue) < Checkpointer.DateTimeMinValue ? Option.Some(Checkpointer.DateTimeMinValue) : lastFailedRevivalTime;
            this.UnhealthySince = unhealthySince.GetOrElse(Checkpointer.DateTimeMinValue) < Checkpointer.DateTimeMinValue ? Option.Some(Checkpointer.DateTimeMinValue) : unhealthySince;
            this.Offset = offset;
        }

        public long Offset { get; }

        public Option<DateTime> LastFailedRevivalTime { get; }

        public Option<DateTime> UnhealthySince { get; }
    }
}
