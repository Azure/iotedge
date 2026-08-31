# Azure IoT Edge Metrics Collector

## About
This repository contains source code for the Azure IoT Edge Metrics module.

## Deployment:
See ExampleDeployment.json for a complete example deployment manifest.


## Setup Steps:
If sending data to Log Analytics (`UploadTarget=AzureMonitor`), metrics are uploaded via the [Logs Ingestion API](https://learn.microsoft.com/azure/azure-monitor/logs/logs-ingestion-api-overview) (the older HTTP Data Collector API is retired on 14 September 2026). Before deploying this module you must:
1. Create a Data Collection Endpoint (DCE).
2. Create (or reuse) a custom table in your Log Analytics workspace with columns matching `Origin`, `Namespace`, `Name`, `Value`, `CollectionTime`, `Tags`, `Computer`.
3. Create a Data Collection Rule (DCR) associated with the DCE and the destination table, and note its immutable ID and stream name.
4. Grant the identity used by this module (see the `Authentication` section below) the `Monitoring Metrics Publisher` role on the DCR.


## Configuration:
All configuration is done through environment variables (see ExampleLayeredDeployment.json for an example). All configuration options are listed below:

Required config items:
- `UploadTarget`
    - Should metrics be sent to Azure Monitor (Log Analytics) or to other IoT Modules (via messages). See the Upload Target section for details.
    - Can be `AzureMonitor` or `IotMessage`
- `ResourceID`
    - ARM resource ID of the IoT Hub this node communicates too.
    

Optional config items:
- `DataCollectionEndpoint`
    - The Data Collection Endpoint (DCE) URL to ingest logs to.
    - Required if `UploadTarget` is set to `AzureMonitor`
    - ex: `https://my-dce-name.eastus-1.ingest.monitor.azure.com`
- `DataCollectionRuleId`
    - The immutable ID of the Data Collection Rule (DCR) that routes data to the destination table.
    - Required if `UploadTarget` is set to `AzureMonitor`
    - ex: `dcr-00000000000000000000000000000000`
- `DataCollectionStreamName`
    - The stream name declared on the DCR for the destination custom table.
    - Required if `UploadTarget` is set to `AzureMonitor`
    - ex: `Custom-InsightsMetrics`
- `AadClientId` / `AadTenantId`
    - Optional. Identify the app registration/managed identity/workload identity to authenticate as. See the `Authentication` section below.
- `AadClientCertificatePath` / `AadClientCertificatePassword`
    - Optional. Path (and password, if the file is password-protected) to a certificate used for certificate-based app registration authentication. See the `Authentication` section below.
- `MetricsEndpointsCSV`
    - List of endpoints to scrape Prometheus metrics from
    - ex: `http://edgeAgent:9600/metrics,http://MetricsSpewer:9417/metrics`
    - Defaults to `http://edgeHub:9600/metrics,http://edgeAgent:9600/metrics`
- `ScrapeFrequencyInSecs`
    - How often to poll the endpoints in MetricsEndpointsCSV. Values are in seconds, so 60 would be every minute
    - Defaults to 300.
- `AllowedMetrics`
    - List of metrics to scrape, all other metrics will be ignored (think whitelist). Set AllowedMetrics to an empty string to disable.
    - See below section on format
    - ex: `docker_container_disk_write_bytes metricToScrape{quantile=0.99} endpoint=http://MetricsSpewer:9417/metrics`
    - Defaults to empty.
- `BlockedMetrics`
    - List of metrics to ignore (think blacklist).
    - BlockedMetrics overrides AllowedMetrics, so a metric will not be reported if it is included in both lists.
    - ex: `metricToIgnore{quantile=0.5} endpoint=http://VeryNoisyModule:9001/metrics docker_container_disk_write_bytes`
    - Defaults to empty.
- `CompressForUpload`
    - If HTTP compression should be used when uploading metrics to Application Insights.
    - This should really only be turned off in extremely CPU-bound applications, if at all.
    - ex: `true`
    - Defaults to true.
- `TransformForIoTCentral`
    - If the metrics data needs to be flattened prior to published as IoT messages.
    - Metrics module emits metrics in Prometheus data format. Metrics are emitted as an array of key/value pair. Enabling IoT       Central to build dashboards, The data needs to be flattened out to match the device template interfaces.
    - Turning this option on, it reduces the data size by 70%
    - This can only be turned on if `UploadTarget` set to `IotMessage`
    - ex: `false`
    - Defaults to false.
- `IotHubConnectFrequency`
    - Frequency at which the module will connect to IoT Hub for adoption profiling statistics
    - Input taken in timespan format
    - ex: `00:12:00`
    - Defaults to every 24 hours
- `AzureDomain`
    - Configurable azure domain which is used to select the AAD authority host (public cloud, Azure Government, or Azure China) for authentication.
    - ex: `azure.us`
    - Defaults to `azure.com`

## Authentication:

When `UploadTarget` is `AzureMonitor`, the module authenticates to Azure AD to call the Logs Ingestion API. Client secrets are not supported. Two options are available:

- **Certificate-based app registration** (recommended for customers): set `AadClientCertificatePath` (and `AadClientCertificatePassword` if needed) along with `AadClientId` and `AadTenantId`.
- **Ambient credentials via `DefaultAzureCredential`** (default, used when `AadClientCertificatePath` is not set): picks up, in order, environment credentials (including workload identity federated tokens), managed identity, and an active Azure CLI session. Set `AadClientId`/`AadTenantId` to disambiguate when multiple identities are available (e.g. a specific user-assigned managed identity or workload identity).

## Upload Target:

Scraped metrics can be uploaded directly to Log Analytics (requires outbound internet connectivity, see Adding the InsightsMetrics Table section), or metrics can be published as IoT messages (useful for local consumption).
Metrics published as IoT messages are emitted as UTF8-encoded json from the endpoint `/messages/modules/<module name>/outputs/metricOutput`. The format is as follows:

```
[{
    "TimeGeneratedUtc": "<time generated>",
    "Name": "<prometheus metric name>",
    "Value": <decimal value>,
    "Label": {
        "<label name>": "<label value>"
    }
}, {
    "TimeGeneratedUtc": "2020-07-28T20:00:43.2770247Z",
    "Name": "docker_container_disk_write_bytes",
    "Value": 0.0,
    "Label": {
        "name": "AzureMonitorForIotEdgeModule"
    }
}]
```

Turning on TransformForIoTCentral, the format of IoT messages changes to:

```
{
  "TimeGeneratedUtc": "<time generated>",
  "edge_device": "<device bame>",
  "instance_number": "<instance number>",
  "<prometheus metric name>": {
    "value": <decimal value>,
    "from": "<value of: 'from', 'module_name', or 'id' if exists>",
    "from_route_output": "<value of: 'from_route_output', 'route_output', or 'source' if exists>",
    "to": "<value of: 'to', 'to_route_input', or 'target' if exists>",
    "priority": <integer value of 'priority' if exists>,
    "quantile": <decimal value of 'quantile' if exists>,
    "command": "<value of 'command' if exists>",
    "disk_name": "<value of 'disk_name' if exists>"
  },
  "edgeAgent_used_memory_bytes": {
    "value": 54992896.0,
    "from": "edgeAgent"
  },
  "edgeAgent_used_memory_bytes": {
    "value": 131170304.0,
    "from": "edgeHub"
  }
}
```


## Allow and Disallow Lists:

`AllowedMetrics` and `BlockedMetrics` are space or comma separated lists of metric selectors. A metric will match the list and be included or excluded if it matches one or more metrics in either list. 

Metric selectors use a format similar to a subset of the PromQl query language. They consist of three parts:

``` metricToSelect{quantile=0.5,otherLabel=~Re[ge]*|x}[http://VeryNoisyModule:9001/metrics] ```

1. Metric name (`metricToSelect`). This component is required.
    - Wildcards `*` (any characters) and `?` (any single character) can be used in metric names. For example, `*CPU` would match `maxCPU` and `minCPU` but not `CPUMaximum`. `???CPU` would match `maxCPU` and `minCPU` but not `maximumCPU`.
    - This component is required.
2. Label based selectors (`{quantile=0.5,otherLabel=~Re[ge]*|x}`). 
    - Multiple metric=value can be included in the curly brackets, they should be comma separated.
    - A metric will be matched if at least all labels in the selector are present and also match.
    - Like PromQl, the following matching operators are allowed.
        - `=` Match labels exactly equal to the provided string (case sensitive).
        - `!=` Match labes not exactly equal to the provided string.
        - `=~` Match labels to a provided regex. ex: `label~=CPU|Mem|[0-9]*`
        - `!=` Match labels which do not fit a provided regex.
    - (Regex note: A [.NET regex engine](https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference) is used. PromQL uses RE2, be ware)
    - Regexs are fully anchroed (A ^ and $ are automatically added to the start and end of each regex)
    - This component is optional.
3. Endpoint selector (`[http://VeryNoisyModule:9001/metrics]`).
    - The URL should exactly match a URL listed in `MetricsEndpointsCSV`.
    - This component is optional.

A metric must match all parts of a given selector to be selected. I.e. a metric must match the name **and** have all the same tags with matching values **and** come from the given endpoint. `mem{quantile=0.5,otherLabel=foobar}[http://VeryNoisyModule:9001/metrics]` would not match the selector `mem{quantile=0.5,otherLabel~=foo|bar}[http://VeryNoisyModule:9001/metrics]`. Multiple selectors should be used to create or-like behavior instead of and-like behavior.


For example, to allow the metric `mem` from the host `host1` regardless of tags but only allow the same metric from `host2` with the tag `agg=p99`, the following selector could added to `AllowedMetrics`:

```
mem{}[http://host1:9001/metrics] mem{agg="p99"}[http://host2:9001/metrics]
```

Or, to allow the metrics `mem` and `cpu` regardless of tags or endpoint, the following could be added to `AllowedMetrics`:
```
mem cpu
```

## Telemetry Notice

This project sends usage and diagnostic data back to Microsoft (CPU and memory usage (of this process), exceptions, number of metrics scraped, scrape frequency). Data collection can be turned off by setting the environment variable DISABLE_TELEMETRY to any value. See the collection statement below.

**Data Collection.** The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.
