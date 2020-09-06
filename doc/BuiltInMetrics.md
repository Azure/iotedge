# IoT Edge Metrics

The IoT Edge Hub and Edge Agent module expose a number of metrics in the Prometheus exposition format that provide insights into its operational state.

## How to enable
### Version 1.0.10+
As of release 1.0.10, metrics are automatically exposed by default on http port **9600** of the Edge Hub and Edge Agent module. 

If you wish to disable metrics, simply set the `MetricsEnabled` environment variable to false.

## Metrics

Note: All metrics contain the following tags

Tag | Description
---|---
iothub | The hub the device is talking to
edge_device | The device id of the current device
instance_number | A Guid representing the current runtime. On restart, all metrics will be reset. This Guid makes it easier to reconcile restarts. 

### EdgeHub
| Name                                                        | Dimensions                                                                                                                                                                           | Description                                                                                                                                                                                              | Type    |
|-------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------|
| `edgehub_gettwin_total`                                     |  `source` (Operation source)<br> `id` (Module ID)                                                                | Total number of GetTwin calls                                                                                                                                                                                           | counter |
| `edgehub_messages_received_total`                           |   `route_output` (Output that sent the message)<br> `id` (Module ID)                                                         | Total number of messages received from clients                                                                                                                                                                           | counter |
| `edgehub_messages_sent_total`                               |  `from` (Message source)<br> `to` (Message destination)<br>`from_route_output` (Output that sent the message)<br> `to_route_input` (Message destination input [empty when "to" is $upstream])<br> `priority` (message priority to destination)                                                          | Total number of messages sent to clients or upstream                                                                                                                                                                        | counter |
| `edgehub_reported_properties_total`                         |  `target`(Update target)<br> `id` (Module ID)                                                                    | Total reported property updates calls                                                                                                                                                                    | counter |
| `edgehub_message_size_bytes`                                |  `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99])                                       | P50, P90, P95, P99, P99.9 and P99.99 message size from clients. Values may be reported as `NaN` if no new measurements are reported for a certain period of time  (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted.                 | summary |
| `edgehub_gettwin_duration_seconds`                          |  `source` (Operation source)<br> `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99])       | P50, P90, P95, P99, P99.9 and P99.99  time taken for get twin operations.  Values may be reported as `NaN` if no  new measurements are reported for a certain  period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted.     | summary |
| `edgehub_message_send_duration_seconds`                     |  `from` (Message source)<br> `to` (Message destination)<br>`from_route_output` (Output that sent the message)<br> `to_route_input` (Message destination input [empty when "to" is $upstream])<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 time taken to send a message. Values may be reported as `NaN`  if no new measurements are reported for a  certain period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted.              | summary |
| `edgehub_message_process_duration_seconds`                     |  `from` (Message source)<br> `to` (Message destination)<br> `priority` (Message priority) <br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 time taken to process a message from the queue. Values may be reported as `NaN`  if no new measurements are reported for a  certain period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted.              | summary |
| `edgehub_reported_properties_update_duration_seconds`       |  `target` (Operation target)<br> `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99])       | P50, P90, P95, P99, P99.9 and P99.99 time taken to update reported properties. Values may be reported as `NaN`  if no new measurements are reported for a certain  period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted. | summary |
| `edgehub_direct_method_duration_seconds`       |  `from` (Caller)<br> `to` (Reciever)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99])       | P50, P90, P95, P99, P99.9 and P99.99 time taken to resolve a direct message. Values may be reported as `NaN`  if no new measurements are reported for a certain  period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted. | summary |
| `edgehub_direct_methods_total`                               |  `from` (Message source)<br> `to` (Message destination)                                                          | Total number of direct messages sent                                                                                                                                                                        | counter |
| `edgehub_queue_length`                                     |  `endpoint` (Message source)<br> `priority` (queue priority)       | Current length of edgeHub's queue for a given priority                                                                                                                                                                                          | gauge |
| `edgehub_messages_dropped_total`                               |  `reason` (no_route, ttl_expiry)<br> `from` (Message source)<br> `from_route_output` (Output that sent the message)<br>                                                          | Total number of messages removed because of reason                                                                                                                                                                        | counter |
| `edgehub_messages_unack_total`                               |  `reason` (storage_failure)<br> `from` (Message source)<br> `from_route_output` (Output that sent the message)<br>                                                          | Total number of messages unack because storage failure                                                                                                                                                                        | counter |
| `edgehub_offline_count_total`                               |  `id` (Module ID)<br>                                                        | Total number of times edgeHub went offline                                                                                                                                                                        | counter | 
| `edgehub_offline_duration_seconds`                                |  `id` (Module ID)<br> `quantile`(Percentile [50, 90, 95, 99, 99.9, 99.99]) | P50, P90, P95, P99, P99.9 and P99.99 time edge hub was offline. Values may be reported as `NaN`  if no new measurements are reported for a  certain period of time (currently 10 minutes). As this is `summary` type, corresponding `_count` and `_sum` counters will be emitted.              | summary |
| `edgehub_operation_retry_total`      |  `id` (Module ID)<br>`operation` (Operation name)                                                        | Total number of times edgeHub operations were retried                                                                                                                                                                        | counter | 
| `edgehub_client_connect_failed_total`                              | `id` (Module ID)<br> `reason` (not authenticated)<br> | Total number of times clients failed to connect to edgeHub                                                                                                                                                                        | counter |                                                              



### EdgeAgent
| Name                                                        | Dimensions                                                                                                                                                                           | Description                                                                                                                                                                                              | Type    |
|-------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------|
| `edgeAgent_total_time_running_correctly_seconds` | `module_name` | The amount of time the module was specified in the deployment and was in the  running state. | Gauge |
| `edgeAgent_total_time_expected_running_seconds` | `module_name` | The amount of time the module was specified in the deployment | Gauge |
| `edgeAgent_module_start_total` | `module_name`, `module_version` | Number of times edgeAgent asked docker to start the module.  | Counter |
| `edgeAgent_module_stop_total` | `module_name`, `module_version` | Number of times edgeAgent asked docker to stop the module. | Counter |
| `edgeAgent_command_latency_seconds` | `command` | How long it took docker to execute the given command. Possible commands are: create, update,  remove, start, stop, restart | Gauge |
| `edgeAgent_iothub_syncs_total` |  | The amount of times edgeAgent attempted to sync its twin with iotHub, both successful and unsuccessful. This incudes both agent requesting a twin and hub notifying of a twin update | Counter |
| `edgeAgent_unsuccessful_iothub_syncs_total` |  | The amount of times edgeAgent failed to sync its twin with iotHub. | Counter |
| `edgeAgent_deployment_time_seconds` |  | The amount of time it took to complete a new deployment after recieving a change. | Counter |
| `edgeagent_direct_method_invocations_count` | `method_name` | Number of times a built-in edgeAgent direct method is called, such as Ping or Restart. | Counter |
|||
| `edgeAgent_host_uptime_seconds` || How long the host has been on | Gauge |
| `edgeAgent_iotedged_uptime_seconds` || How long iotedged has been running | Gauge |
| `edgeAgent_available_disk_space_bytes` | `disk_name`, `disk_filesystem`, `disk_filetype` | Amount of space left on the disk | Gauge |
| `edgeAgent_total_disk_space_bytes` | `disk_name`, `disk_filesystem`, `disk_filetype`| Size of the disk | Gauge |
| `edgeAgent_used_memory_bytes` | `module_name` | Amount of RAM used by all processes | Gauge |
| `edgeAgent_total_memory_bytes` | `module_name` | RAM available | Gauge |
| `edgeAgent_used_cpu_percent` | `module_name` | Percent of cpu used by all processes | Histogram |
| `edgeAgent_created_pids_total` | `module_name` | The number of processes or threads the container has created | Gauge |
| `edgeAgent_total_network_in_bytes` | `module_name` | The amount of bytes recieved from the network | Gauge |
| `edgeAgent_total_network_out_bytes` | `module_name` | The amount of bytes sent to network | Gauge |
| `edgeAgent_total_disk_read_bytes` | `module_name` | The amount of bytes read from the disk | Gauge |
| `edgeAgent_total_disk_write_bytes` | `module_name` | The amount of bytes written to disk | Gauge |
|||
| `edgeAgent_metadata` | `edge_agent_version`, `experimental_features`, `host_information` | General metadata about the device. The value is always 0, information is encoded in the tags. Note `experimental_features` and `host_information` are json objects. `host_information` looks like ```{"OperatingSystemType": "linux", "Architecture": "x86_64", "Version": "1.0.10~dev20200803.4", "ServerVersion": "19.03.6", "KernelVersion": "5.0.0-25-generic", "OperatingSystem": "Ubuntu 18.04.4 LTS", "NumCpus": 6}```. Note `ServerVersion` is the Docker version and `Version` is the IoT Edge Security Daemon version. | Gauge |


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