// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    /// <summary>
    /// Actions to be run when entering and/or exiting a state.
    /// These actions are run regardless of the command triggering
    /// the transition.
    /// </summary>
    class StateActions
    {
        public Func<EndpointExecutorFsm, Task> Enter { get; }

        public Func<EndpointExecutorFsm, Task> Exit { get; }

        public StateActions()
            : this(NullAction, NullAction)
        {
        }

        public StateActions(Func<EndpointExecutorFsm, Task> enter, Func<EndpointExecutorFsm, Task> exit)
        {
            this.Enter = enter;
            this.Exit = exit;
        }

        public static readonly StateActions Null = new StateActions();

        public static Task NullAction(EndpointExecutorFsm machine) => TaskEx.Done;
    }
}