// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;

    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class EndpointExecutorStatus
    {
        public EndpointExecutorStatus(string id, State state, int retryAttempts, TimeSpan retryPeriod, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CheckpointerStatus checkpointerStatus)
        {
            this.Id = id;
            this.State = state;
            this.RetryAttempts = retryAttempts;
            this.RetryPeriod = retryPeriod;
            this.LastFailedRevivalTime = lastFailedRevivalTime;
            this.UnhealthySince = unhealthySince;
            this.CheckpointerStatus = checkpointerStatus;
        }

        public CheckpointerStatus CheckpointerStatus { get; }

        public string Id { get; }

        public Option<DateTime> LastFailedRevivalTime { get; }

        public int RetryAttempts { get; }

        public TimeSpan RetryPeriod { get; }

        public State State { get; }

        public Option<DateTime> UnhealthySince { get; }
    }
}
