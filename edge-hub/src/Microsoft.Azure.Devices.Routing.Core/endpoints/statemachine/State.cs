// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
