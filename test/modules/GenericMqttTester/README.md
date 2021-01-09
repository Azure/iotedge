# Generic MQTT Test Module

This module is designed to test generic (non-iothub) mqtt telemetry in both a single-node and nested environment. The module will run in one of two modes. The behavior depends on this mode.

1: Initiate mode
- If nested scenario, test module runs on the lowest node in the topology.
- Spawn a thread that publishes messages continuously to upstream edge.
- Receives same message routed back from upstream edge and reports the result to the Test Result Coordinator test module.

2: Relay mode
- If nested scenario, test module runs on the middle node in the topology.
- Receives a message from downstream edge and relays it back to downstream edge.
