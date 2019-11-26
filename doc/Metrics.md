
# **!!! IMPORTANT NOTE !!! The metrics described here are deprecated and will be removed in subsequent releases. They are superseded by [edge metrics V2](edge-metrics-v2.md).**

## Metrics in EdgeHub

EdgeHub is intrumented to collect the following metrics:

| MetricName        | Description           | Unit  |
| ------------- |:-------------:|:-----:|
| EdgeHubToCloudMessageLatencyMs | Time taken by EdgeHub to send a message to the cloud | Milliseconds** |
| EdgeHubToCloudMessageSentCount | Number of messages sent by EdgeHub to the cloud | Count resets each reporting interval* |
| EdgeHubConnectedClientGauge | Number of clients/devices currently connected to EdgeHub | Count |
| EndpointMessageStoredLatencyMs | Time taken by EdgeHub to acknowledge receipt of a message | Milliseconds** |
| EndpointMessageStoredCount | Total number of messages stored by EdgeHub | Last recorded total | 
| EndpointMessageDrainedCount | Total number of messages sent to a message endpoint by EdgeHub | Last recorded total |
| MessageEntityStorePutOrUpdateLatencyMs | Time taken by EdgeHub to record a message in an internal reference counting db store | Milliseconds** |
| SequentialStoreAppendLatencyMs | Time taken by EdgeHub to store a message in an append log | Milliseconds** | 
| DbGetLatencyMs | Time taken by EdgeHub to get a message from the store-and-forward db | Milliseconds** | 
| DbPutLatencyMs | Time taken by EdgeHub to write a message to the store-and-forward db | Milliseconds** | 

\* EdgeHub reports metrics to InfluxDb every 5s. Counters are reset after each reporting interval so that if the time series is summed up over a large interval, it returns the true sum as opposed to a sum of sums.

\** Latency measurements are recorded per operation being measured. The Edge runtime does not aggregate measurements. The measurements are reported to InfluxDb at a regular interval, currently set to 5s. Aggregations can be done via queries from the database.

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
      "createOptions": ""
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
After the modules are deployed, Chronograf can be reached at http://localhost:8888. InfluxDb should be configured as a data source for Chronograf. The InfluxDb instance should be reachable at http://influxdb:8086.

## Example InfluxDb queries

For example InfluxDb queries, please look at https://github.com/Azure/iotedge/blob/master/tools/snitch/snitcher/src/settings.yaml#L16

## Collecting CPU and memory stats from EdgeHub and other containers

A telegraf container can be added to the deployment to collect stats about all containers in the deployment. Telegraf uses a configuration file on the device to configure itself. Azure IoT Edge deployments can only mount files in to a container but cannot pass a configuration file to the device from the cloud. For this reason, the configuration has to be done on the device as follows:
```
mkdir telegraf
docker run --rm telegraf telegraf config > telegraf/telegraf.conf
```
Edit the file to reflect the following changes:
```
[[outputs.influxdb]]
    urls = ["http://influxdb:8086"]
[[inputs.docker]]
  endpoint = "unix:///var/run/docker.sock"
```
The telegraf container can be run manually on the device as follows:
```
docker run -d --name=telegraf --net=azure-iot-edge -v /var/run/docker.sock:/var/run/docker.sock -v $PWD/telegraf/telegraf.conf:/etc/telegraf/telegraf.conf:ro telegraf
```
Or the container can be added to an Azure IoT Edge deployment using the following deployment information:
```
"telegraf": {
    "type": "docker",
    "settings": {
    "image": "registry.hub.docker.com/library/telegraf:latest",
    "createOptions": "{\"HostConfig\":{\"Binds\":[\"/var/run/docker.sock:/var/run/docker.sock\",\"/home/jadsa/telegraf/telegraf.conf:/etc/telegraf/telegraf.conf\"]}}"
    },
    "version": "1.0",
    "status": "running",
    "restartPolicy": "always"
}
```
After telegraf is running on the device it will emit metrics about the docker containers. These metrics can be viewed in Chronograf under the telegraf.autogen metrics database.
More details on telegraf docker images can be found at https://hub.docker.com/_/telegraf/.

## Setting up alerts

Alerts can be setup using the Kapacitor component of the TICK stack. Kapacitor can be run manually on the device as follows:
```
mkdir kapacitor
docker run --rm kapacitor kapacitord config > kapacitor/kapacitor.conf
docker run -d --name=kapacitor --net=azure-iot-edge -p 9092:9092 -v $PWD/kapacitor/kapacitor.conf:/etc/kapacitor/kapacitor.conf:ro kapacitor
```
Alternatively, the container can be run via an Azure IoT Edge deployment using the following deployment information:
```
"kapacitor": {
    "type": "docker",
    "settings": {
    "image": "registry.hub.docker.com/library/kapacitor:latest",
            "createOptions": "{\"HostConfig\":{\"Binds\":[\"/home/jadsa/kapacitor/kapacitor.conf:/etc/kapacitor/kapacitor.conf\"]}}"
    },
    "status": "running",
    "restartPolicy": "always",
    "version": "1.0"
}
```
Details on setting up alerts using Kapacitor can be found at https://docs.influxdata.com/kapacitor/v1.5/working/kapa-and-chrono/. More details on kapacitor docker images can be found at https://hub.docker.com/_/kapacitor/.
