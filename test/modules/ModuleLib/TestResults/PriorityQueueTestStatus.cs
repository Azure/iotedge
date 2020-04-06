// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class PriorityQueueTestStatus
    {
        public PriorityQueueTestStatus(bool isFinished, int resultCount)
        {
            this.IsFinished = isFinished;
            this.ResultCount = resultCount;
        }

        public bool IsFinished { get; }

        public int ResultCount { get;  }
    }
}
