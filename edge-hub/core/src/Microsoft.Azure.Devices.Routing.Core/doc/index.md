## Endpoint Executor State Machine
The following diagram describes the state machine for delivering messages
to an endpoint. The state machine allows the definition of the
endpoint to be updated failing. The implementation is handled by the
`EndpointExecutorFsm` in `Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine`.

In the implementation, actions can be run when entering and exiting a state,
regardless of the command that triggered the transition, or for a specific transition
(current state/command pair).

![][1]

[1]: img/failure.png