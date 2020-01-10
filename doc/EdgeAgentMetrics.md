Name | Additional Tags | Description | Type
--- | --- | --- | ---
edgeAgent_total_time_running_correctly_seconds | module_name | The amount of time the module was specified in the deployment and was in the running state. | Gauge
edgeAgent_total_time_expected_running_seconds | module_name | The amount of time the module was specified in the deployment | Gauge
edgeAgent_module_start_total | module_name, module_version | Number of times edgeAgent asked docker to start the module.  | Counter
edgeAgent_module_stop_total | module_name, module_version | Number of times edgeAgent asked docker to stop the module. | Counter
edgeAgent_command_latency_seconds | command | How long it took docker to execute the given command. Possible commands are: create, update, remove, start, stop, restart | Gauge
|||
edgeAgent_host_uptime_seconds | | How long the host has been on | Gauge
edgeAgent_iotedged_uptime_seconds || How long iotedged has been running | Gauge
edgeAgent_available_disk_space_bytes | disk_name, disk_filesystem, disk_filetype | Amount of space left on the disk | Gauge
edgeAgent_total_disk_space_bytes |disk_name, disk_filesystem, disk_filetype| Size of the disk | Gauge
edgeAgent_used_memory_bytes | module | Amount of RAM used by all processes | Gauge
edgeAgent_total_memory_bytes | module | RAM available | Gauge
edgeAgent_used_cpu_percent | module | Percent of cpu used by all processes | Histogram
edgeAgent_created_pids_total | module | The number of processes or threads the container has created | Gauge
edgeAgent_total_network_in_bytes | module | The amount of bytes recieved from the network | Gauge
edgeAgent_total_network_out_bytes | module | The amount of bytes sent to network | Gauge
edgeAgent_total_disk_read_bytes | module | The amount of bytes read from the disk | Gauge
edgeAgent_total_disk_write_bytes | module | The amount of bytes written to disk | Gauge

Note: All metrics contain the following tags
Tag | Description
---|---
iothub | The hub the device is talking to
edge_device | The device id of the current device
instance_number | The number of times edgeAgent has been restarted. On restart, all metrics will be reset. This makes it easier to reconcile restarts. 