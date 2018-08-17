## Metrics in EdgeHub

EdgeHub is intrumented to collect the following metrics:

| MetricName        | Description           | Unit  |
| ------------- |:-------------:|:-----:|
| EdgeHubToCloudMessageLatencyMs | Time taken by EdgeHub to send a message to the cloud | Milliseconds |
| EdgeHubToCloudMessageSentCount | Number of messages sent by EdgeHub to the cloud | Count per reporting interval* |
| EndpointMessageStoredLatencyMs | Time taken by EdgeHub to acknowledge receipt of a message | Milliseconds |
| EndpointMessageStoredCount | Number of messages stored by EdgeHub | Count per reporting interval* | 
| MessageEntityStorePutOrUpdateLatencyMs | Time taken by EdgeHub to record a message in an internal reference counting db store | Milliseconds |
| SequentialStoreAppendLatencyMs | Time taken by EdgeHub to store a message in an append log | Milliseconds | 
| DbGetLatencyMs | Time taken by EdgeHub to get a message from the store-and-forward db | Milliseconds | 
| DbPutLatencyMs | Time taken by EdgeHub to write a message to the store-and-forward db | Milliseconds | 

\* EdgeHub reports metrics to InfluxDb every 20s. Counters are reset after each reporting interval so that if the time series is summed up over a large interval, it returns the true sum as opposed to a sum of sums.

## Configuring EdgeHub to record metrics

EdgeHub can be configured to record metrics by setting an environment variable called **'CollectMetrics'** to **'true'**. 
This can be done via the portal in the 'Configure advanced Edge Runtime settings' section. If CollectMetrics is set to true, the default 
storage location for metrics is an InfluxDb container running on the same docker network as EdgeHub.

The following defaults can be modified by setting environment variables in EdgeHub:

| Environment variable | Description | Default |
|----------------------|:-----------:|:--------:|
| Metrics__MetricsDbName | Name of metrics database in InfluxDb | metricsdatabase |
| Metrics__InfluxDbUrl | Network address of InfluxDb | http://influxdb:8086 |

## Creating a deployment for metrics collection

Besides setting the EdgeHub environment variables as described in the previous section, an InfluxDb container needs to be added to the
deployment as a module with the following configuration:

```
"influxdb": {
  "type": "docker",
  "settings": {
    "image": "registry.hub.docker.com/library/influxdb:latest",
    "createOptions": "{\r\n \"PortBindings\": {\r\n \"8086/tcp\": [\r\n {\r\n \"HostPort\": \"8086\"\r\n }\r\n ],\r\n \"8083/tcp\": [\r\n {\r\n \"HostPort\": \"8083\"\r\n }\r\n ]\r\n }\r\n}"
  },
  "version": "1.0",
  "status": "running",
  "restartPolicy": "always"
}
```
## Viewing metrics from EdgeHub

Metrics captured by EdgeHub can be viewed using Chronograf (https://www.influxdata.com/time-series-platform/chronograf/). A Chronograf module can be 
added to the deployment with the following parameters:

```
"chronograf": {
  "type": "docker",
  "settings": {
    "image": "registry.hub.docker.com/library/chronograf:latest",
    "createOptions": "{\r\n \"PortBindings\": {\r\n \"8888/tcp\": [\r\n {\r\n \"HostPort\": \"8888\"\r\n }]\r\n }\r\n}"
  },
  "status": "running",
  "restartPolicy": "always",
  "version": "1.0"
}
```
After the modules are deployed, Chronograf can be reached at localhost:8888.

## Example InfluxDb queries

For example InfluxDb queries, please look at https://github.com/Azure/iotedge/pull/141/files#diff-9fc75ceaff4ae01d42e1bafad8037848

## Collecting CPU and memory stats from EdgeHub and other containers

A telegraf container can be added to the deployment to collect stats about all containers in the deployment. Please refer to https://hub.docker.com/_/telegraf/ for steps to configure it.  
