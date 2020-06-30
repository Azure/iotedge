// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine
{
    public enum State
    {
        Start,
        Idle,
        Sending,
        Checkpointing,
        Failing,
        DeadIdle,
        Closed,
        DeadProcess,
        DeadCheckpointing,
    }
}