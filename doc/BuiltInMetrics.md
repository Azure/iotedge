# IoT Edge Metrics

The IoT Edge Hub and Edge Agent module expose a number of metrics in the Prometheus exposition format that provide insights into its operational state.

## How to enable

As of release 1.0.9, metrics are exposed as an experimental feature available at http port **9600** of the Edge Hub and Edge Agent module. To enable, the following environment variables should be set for each module (make note of the double underscores):

| Environment Variable Name | value |
|------------------------------------------|--------|
| `ExperimentalFeatures__Enabled` | `true` |
| `ExperimentalFeatures__EnableMetrics` | `true` |

### Windows Note (1.0.9 only)
Metrics on Windows are not fully supported the 1.0.9 experimental release. Host metrics (cpu, memory and disk usage) will all show as 0. Moby metrics are supported. In addition, edgeHub must be started as a container administrator to expose metrics. 

This is no longer required in the 1.0.10 release

Add the following to EdgeHub createOptions (on Windows only):
```JSON
createOptions: {
 "User": "ContainerAdministrator",
 "ExposedPorts": {}
}
```

## Metrics

Note: All metrics contain the following tags

Tag | Description
---|---
iothub | The hub the device is talking to
edge_device | The device id of the current device
instance_number | A Guid representing the current runtime. On restart, all metrics will be reset. This Guid makes it easier to reconcile restarts. 

### EdgeHub
| Name | Dimensions | Description | Type | Range |
|-|-|-|-|-|
| `edgehub_gettwin_total` | `source` (Operation source)<br> `id` (Module ID) | Total number of GetTwin calls | `Counter` | `[0, )` |
| `edgehub_messages_received_total` | `route_output` (Output that sent the message)<br> `id` (Module ID) | Total number of messages received from clients | `Counter` | `[0, )` |
| `edgehub_messages_sent_total` | `from` (Message source)<br> `to` (Message destination)<br>`from_route_output` (Output that sent the message)<br> `to_route_input` (Message destination input [empty when "to" is $upstream]) | Total number of messages sent to clients or upstream | `Counter` | `[0, )` |
| `edgehub_reported_properties_total` | `target`(Update target)<br> `id` (Module ID) | Total reported property updates calls | `Counter` | `[0, )` |
| `edgehub_message_size_bytes` | `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 message size from clients. Values may be reported as `NaN` if no new measurements are reported for a certain period of time (currently 10 minutes). As this is `Histogram` type, corresponding `_count` and `_sum` counters will be emitted. | `Histogram` | `(0, 256000]` |
| `edgehub_gettwin_duration_seconds` | `source` (Operation source)<br> `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 time taken for get twin operations. Values may be reported as `NaN` if no new measurements are reported for a certain period of time (currently 10 minutes). As this is `Histogram` type, corresponding `_count` and `_sum` counters will be emitted. | `Histogram` |`(0, )` |
| `edgehub_message_send_duration_seconds` | `from` (Message source)<br> `to` (Message destination)<br>`from_route_output` (Output that sent the message)<br> `to_route_input` (Message destination input [empty when "to" is $upstream])<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 time taken to send a message. Values may be reported as `NaN` if no new measurements are reported for a certain period of time (currently 10 minutes). As this is `Histogram` type, corresponding `_count` and `_sum` counters will be emitted. | `Histogram` | `(0, )` |
| `edgehub_reported_properties_update_duration_seconds` | `target` (Operation target)<br> `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 time taken to update reported properties. Values may be reported as `NaN` if no new measurements are reported for a certain period of time (currently 10 minutes). As this is `Histogram` type, corresponding `_count` and `_sum` counters will be emitted. | `Histogram` |`(0, )` |
| `edgehub_direct_method_duration_seconds` | `from` (Caller)<br> `to` (Reciever)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 time taken to resolve a direct message. Values may be reported as `NaN` if no new measurements are reported for a certain period of time (currently 10 minutes). As this is `Histogram` type, corresponding `_count` and `_sum` counters will be emitted. | `Histogram` |`(0, 300]` |
| `edgehub_direct_methods_total` | `from` (Message source)<br> `to` (Message destination) | Total number of direct messages sent | `Counter` |`(0, )` |
| `edgehub_queue_length` | `endpoint` (Message source)<br> | Current length of edgeHub's queue | `Gauge` |`[0, )` |


### EdgeAgent
| Name | Dimensions | Description | Type | Range |
|-|-|-|-|-|
| `edgeAgent_total_time_running_correctly_seconds` | `module_name` | The amount of time the module was specified in the deployment and was in the running state. | `Gauge` | `(0, )` |
| `edgeAgent_total_time_expected_running_seconds` | `module_name` | The amount of time the module was specified in the deployment | `Gauge` | `(0, )` |
| `edgeAgent_module_start_total` | `module_name`, `module_version` | Number of times edgeAgent asked docker to start the module. | `Counter` | `[0, )` |
| `edgeAgent_module_stop_total` | `module_name`, `module_version` | Number of times edgeAgent asked docker to stop the module. | `Counter` | `[0, )` |
| `edgeAgent_command_latency_seconds` | `command` | How long it took docker to execute the given command. Possible commands are: create, update, remove, start, stop, restart | `Histogram` | `[0, )` |
|||
| `edgeAgent_host_uptime_seconds` || How long the host has been on | `Gauge` | `(0, )` |
| `edgeAgent_iotedged_uptime_seconds` || How long iotedged has been running | `Gauge` | `(0, )` |
| `edgeAgent_available_disk_space_bytes` | `disk_name`, `disk_filesystem`, `disk_filetype` | Amount of space left on the disk | `Gauge` | `(0, )` |
| `edgeAgent_total_disk_space_bytes` | `disk_name`, `disk_filesystem`, `disk_filetype`| Size of the disk | `Gauge` | `(0, )` |
| `edgeAgent_used_memory_bytes` | `module_name` | Amount of RAM used by all processes | `Gauge` | `(0, )` |
| `edgeAgent_total_memory_bytes`* | `module_name` | RAM available | `Gauge` | `(0, )` |
| `edgeAgent_used_cpu_percent` | `module_name` | Percent of cpu used by all processes | `Histogram` | `[0, 100]` |
| `edgeAgent_created_pids_total`* | `module_name` | The number of processes or threads the container has created | `Gauge` | `(0, )` |
| `edgeAgent_total_network_in_bytes` | `module_name` | The amount of bytes recieved from the network | `Gauge` | `(0, )` |
| `edgeAgent_total_network_out_bytes` | `module_name` | The amount of bytes sent to network | `Gauge` | `(0, )` |
| `edgeAgent_total_disk_read_bytes` | `module_name` | The amount of bytes read from the disk | `Gauge` | `(0, )` |
| `edgeAgent_total_disk_write_bytes` | `module_name` | The amount of bytes written to disk | `Gauge` | `(0, )` |

* These metrics are not collected by docker on windows

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