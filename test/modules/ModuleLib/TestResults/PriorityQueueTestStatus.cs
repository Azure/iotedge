// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class PriorityQueueTestStatus
    {
        public PriorityQueueTestStatus(bool isFinished, int resultCount)
        {
            this.IsFinished = Preconditions.CheckNotNull(isFinished, nameof(isFinished));
            this.ResultCount = Preconditions.CheckNotNull(resultCount, nameof(resultCount));
        }

        public bool IsFinished { get; }

        public int ResultCount { get;  }
    }
}
