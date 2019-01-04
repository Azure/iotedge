// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    /// <summary>
    /// Stores next state and optional action to be run during this specific
    /// transition. This is the value in the state transition table.
    /// </summary>
    class StateTransition
    {
        public StateTransition(State nextState)
            : this(nextState, NullAction)
        {
        }

        public StateTransition(State nextState, Func<EndpointExecutorFsm, ICommand, Task> transitionAction)
        {
            this.NextState = nextState;
            this.TransitionAction = transitionAction;
        }

        public State NextState { get; }

        public Func<EndpointExecutorFsm, ICommand, Task> TransitionAction { get; }

        public static Task NullAction(EndpointExecutorFsm machine, ICommand command) => TaskEx.Done;
    }
}
