# Built-in logs collation and upload capability

IoT Edge supports native retrieval of module logs and upload to Azure Blob Storage. Users can access this functionality via direct method calls to the Edge Agent module. The following methods are available in support of this:

- UploadLogs
- GetTaskStatus

## Feature enabling

As of [1.0.8 release](https://github.com/Azure/azure-iotedge/releases/tag/1.0.8) of IoT Edge, this capability is available as an experimental feature. To enable it, the following environment variables need to be set for the edgeAgent (make note of the double underscores):

| Environment Variable Name                | value  |
|------------------------------------------|--------|
| `ExperimentalFeatures__Enabled`          | `true` |
| `ExperimentalFeatures__EnableUploadLogs` | `true` |

> Due to certain design limitations, settings for the Edge Agent do not take affect unless its image is also changed (as of 1.0.8 release). If you are using the default Edge Agent settings, change the image to use a specific tag along with the environment variables above, e.g. `mcr.microsoft.com/azureiotedge-agent:1.0.8`.

## Recommended logging format

For best compatibility with this feature, the recommended logging format is:

```
<{LogLevel}> {Timestamp} {MessageText}
```

`{LogLevel}` should follow the [Syslog severity level format](https://wikipedia.org/wiki/Syslog#Severity_lnevel) and `{Timestamp}` should be formatted as `yyyy-mm-dd hh:mm:ss.fff zzz`.

The [Logger class in IoT Edge](https://github.com/Azure/iotedge/blob/master/edge-util/src/Microsoft.Azure.Devices.Edge.Util/Logger.cs) serves as a canonical implementation.


## UploadLogs

This method accepts a JSON payload with the following schema:

    {    
        "schemaVersion": "1.0",
        "sasUrl": "Full path to SAS URL",
        "items": [
            {
                "id": "regex string", 
                "filter": {
                    "tail": int, 
                    "since": int,
                    "loglevel": int, 
                    "regex": "regex string" 
                }
            }
        ],
        "encoding": "gzip/none",
        "contentType": "json/text" 
    }

| Name          | Type         | Description                                                                                                                                                                                                                                          |
|---------------|--------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| schemaVersion | string       | Set to `1.0`                                                                                                                                                                                                                                         |
| sasURL        | string (URI) | [Shared Access Signature URL with write access to Azure Blob Storage container](https://blogs.msdn.microsoft.com/jpsanders/2017/10/12/easily-create-a-sas-to-download-a-file-from-azure-storage-using-azure-storage-explorer/).                                        |
| items         | JSON array   | An array with `id` and `filter` tuples.                                                                                                                                                                            |
| id            | string       | A regular expression that supplies the module name. It can match multiple modules on an edge device. [.NET Regular Expressions](https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions) format is expected.                 |
| filter        | JSON section | Log filters to apply to the modules matching the `id` regular expression in the tuple.                                                                                                                                                               |
| tail          | integer      | Number of log lines in the past to retrieve starting from the latest. OPTIONAL.                                                                                                                                                                               |
| since         | integer      | Starting point in time to retrieve logs from to the latest expressed as [Unix time](https://en.wikipedia.org/wiki/Unix_time). Tools like [epoch converter](https://www.epochconverter.com) can help in converting from a more human readable format. If both `tail` and `since` are specified, first the logs using the `since` value are retrieved and then `tail` value of those are returned. OPTIONAL.|
| loglevel      | integer      | Filter log lines less than or equal to specified loglevel. Log lines should follow recommended logging format and use [Syslog severity level](https://en.wikipedia.org/wiki/Syslog#Severity_level) standard. OPTIONAL.                                                                                                                |
| regex         | string       | Filter log lines which have content that match the specified regular expression using [.NET Regular Expressions](https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions) format. OPTIONAL.                                            |
| encoding      | string       | Either `gzip` or `none`. Default is `none`.                                                                                                                                                                                                          |
| contentType   | string       | Either `json` or `text`. Default is `text`.                                                                                                                                                                                                          |

### Response

The response is returned with the following schema:

```
{ 
    "status": "string", 
    "message": "string", 
    "correlationId": "GUID"
} 
```

| Name          | Type   | Description                                                        |
|---------------|--------|--------------------------------------------------------------------|
| status        | string | One of `NotStarted`, `Running`, `Completed`, `Failed` or `Unknown`. |
| message       | string | Message in case of error, empty string otherwise.                  |
| correlationId | string   | ID to query to status of the upload request.      |


### Examples using `az` cli (bash)

**Upload last 100 log lines from all modules, in compressed JSON format:**

```shell
az iot hub invoke-module-method -n <replace-with-hub> -d <replace-with-device-id> -m \$edgeAgent --mn UploadLogs --mp \
'
    {
        "schemaVersion": "1.0",
        "sasUrl": "https://xyz.blob.core.windows.net/abc?st=2019-06-06T05%3A11%3A56Z&se=2019-06-11T05%3A11%3A00Z&sp=abc=2018-03-28&sr=c&sig=xyz",
        "items": [
            {
                "id": ".*", 
                "filter": {
                    "tail": 100
                }
            }
        ],
        "encoding": "gzip", 
        "contentType": "json"
    }
'
```

**Upload last 100 log lines from edgeAgent and edgeHub with last 1000 log lines from tempSensor module in uncompressed text format**
```
az iot hub invoke-module-method -n <replace-with-hub> -d <replace-with-device-id> -m \$edgeAgent --mn UploadLogs --mp \
'
    {
        "schemaVersion": "1.0",
        "sasUrl": "https://xyz.blob.core.windows.net/abc?st=2019-06-06T05%3A11%3A56Z&se=2019-06-11T05%3A11%3A00Z&sp=abc=2018-03-28&sr=c&sig=xyz",
        "items": [
            {
                "id": "edge", 
                "filter": {
                    "tail": 100
                }
            },
            {
                "id": "tempSensor",
                "filter": {
                    "tail": 1000
                }
            }
        ],
        "encoding": "none", 
        "contentType": "text"
    }
'
```

## GetTaskStatus

The status of upload logs request can be queried using the `correlationId` returned in response the `UploadLogs` direct method call. The `correlationId` is specified in the request to `GetTaskStatus` direct method on the edgeAgent using the following schema:

```
{ 
  "schemaVersion": "1.0", 
  "correlationId": "<GUID>” 
} 
```

| Name          | Type   | Description                                                        |
|---------------|--------|--------------------------------------------------------------------|
| SchemaVersion | string | Set to `1.0`                                                       |
| correlationId | string   | `correlationId` GUID from the `UploadLogs` direct method response. |

The response is in the same format as `UploadLogs`.
