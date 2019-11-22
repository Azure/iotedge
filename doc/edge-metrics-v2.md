# IoT Edge Metrics

## EdgeHub

The IoT Edge Hub module exposes a number of metrics in the Prometheus exposition format that provide insights into its operational state.

### How to enable

As of release 1.0.9, metrics are exposed as an experimental feature available at http port **9600** of the Edge Hub module. To enable, the following environment variables should be set for the module (make note of the double underscores):

| Environment Variable Name                | value  |
|------------------------------------------|--------|
| `ExperimentalFeatures__Enabled`          | `true` |
| `ExperimentalFeatures__EnableMetrics`    | `true` |

### Metrics

| Name                                                        | Dimensions                                                                                                                                                                           | Description                                                                                                                                                                                              | Type    |
|-------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------|
| `edgehub_gettwin_total`                                     | `iothub` (IoT Hub Name)<br> `edge_device` (IoT Edge device name)<br> `source` (Operation source)<br> `id` (Module ID)                                                                | Total number of GetTwin calls                                                                                                                                                                                           | counter |
| `edgehub_messages_received_total`                           | `iothub` (IoT Hub Name)<br> `edge_device` (IoT Edge device name)<br>  `protocol` (Protocol received on)<br> `id` (Module ID)                                                         | Total number of messages received from clients                                                                                                                                                                           | counter |
| `edgehub_messages_sent_total`                               | `iothub` (IoT Hub Name)<br> `edge_device` (IoT Edge device name)<br> `from` (Message source)<br> `to` (Message destination)                                                          | Total number of messages sent to clients or upstream                                                                                                                                                                        | counter |
| `edgehub_reported_properties_total`                         | `iothub` (IoT Hub Name)<br> `edge_device` (IoT Edge device name)<br> `target`(Update target)<br> `id` (Module ID)                                                                    | Total reported property updates calls                                                                                                                                                                    | counter |
| `edgehub_message_size_bytes`                                | `iothub` (IoT Hub Name)<br> `edge_device` (IoT Edge device name)<br> `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99])                                       | P50, P90, P95, P99, P99.9 and P99.99 message size from clients. Values may be reported as `NaN` if no new measurements are reported for a certain period of time  (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted.                 | summary |
| `edgehub_gettwin_duration_seconds`                          | `iothub` (IoT Hub Name)<br> `edge_device` (IoT Edge device name)<br> `source` (Operation source)<br> `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99])       | P50, P90, P95, P99, P99.9 and P99.99  time taken for get twin operations.  Values may be reported as `NaN` if no  new measurements are reported for a certain  period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted.     | summary |
| `edgehub_message_send_duration_seconds`                     | `iothub` (IoT Hub Name)<br> `edge_device` (IoT Edge device name)<br> `from` (Message source)<br> `to` (Message destination)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 time taken to send a message. Values may be reported as `NaN`  if no new measurements are reported for a  certain period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted.              | summary |
| `edgehub_reported_properties_update_duration_seconds`       | `iothub` (IoT Hub Name)<br> `edge_device` (IoT Edge device name)<br> `target` (Operation target)<br> `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99])       | P50, P90, P95, P99, P99.9 and P99.99 time taken to update reported properties. Values may be reported as `NaN`  if no new measurements are reported for a certain  period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted. | summary |
|                                                             |                                                                                                                                                                                      |                                                                                                                                                                                                          |         |
|                                                             |                                                                                                                                                                                      |                                                                                                                                                                                                          |         |
|                                                             |                                                                                                                                                                                      |                                                                                                                                                                                                          |         |
|                                                             |                                                                                                                                                                                      |                                                                                                                                                                                                          |         |

### Collecting

Metrics will be available for collection at `http://edgeHub:9600/metrics` on the IoT Edge module network or `http://localhost:9600/metrics` if port mapped to the host at the default port number. 

For mapping to host, the port will need to be exposed from Edge Hub's `createOptions`:

```
{
  "ExposedPorts": {
    "9600/tcp": {},
  }
<Other options, if any>
}
```