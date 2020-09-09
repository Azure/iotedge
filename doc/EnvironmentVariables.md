## EdgeAgent
| Variable                             | Description                                                                                                              |
|--------------------------------------|--------------------------------------------------------------------------------------------------------------------------|
| BackupConfigFilePath                 | Path to put the backup deployment config file                                                                            |
| CloseCloudConnectionOnIdleTimeout    | Whether the upstream connection should be closed when CloudConnectionIdleTimeoutSecs is reached, defaults to **false**   |
| CloudConnectionIdleTimeoutSecs       | If there are no IoT operations, time span to wait before the upstream connection is considered idle, defaults to **300** |
| ConfigRefreshFrequencySecs           | Interval at which the EdgeAgent config is refreshed from upstream, defaults to **3600**                                  |
| ConfigSource                         | **twin / local**, specifies where the deployment config should be read from                                              |
| CoolOffTimeUnitInSeconds             | Time span to wait between restart attempts on a module, defaults to **10**, max at **300**                               |
| EnableK8sServiceCallTracing          | Whether to enable logging for K8s requests that the Agent makes                                                          |
| https_proxy                          | Address of the proxy to use for outbound HTTPS requests                                                                  |
| IntensiveCareTimeInMinutes           | Time span for a module to be running before considered completely healthy (restart time / count cleared)                 |
| K8sNamespace                         | K8s namespace to use for deploying modules                                                                               |
| LocalConfigPath                      | Path to local .json file containing Agent config, defaults to **.\config.json**                                          |
| MaxRestartCount                      | Max number of restarts allowed before a module is considered to have failed                                              |
| MetricScrapeInterval                 | Interval at which diagnostic metrics are sampled, defaults to **1 hour**                                                 |
| MetricUploadInterval                 | Interval at which diagnostic metrics are uploaded, defaults to **1 day**                                                 |
| Mode                                 | specifies the mode for module deployment, **iotedged / docker / kubernetes**                                             |
| PerformanceMetricsUpdateFrequency    | Interval to sample system performance metrics, defaults to **5 minutes**                                                 |
| PersistentVolumeClaimDefaultSizeInMb | Size of the PersistedVolumeClaim, must be used with StorageClassName                                                     |
| RequestTimeoutSecs                   | Timeout for handling ping and GetTaskStatus direct methods, defaults to **600**                                          |
| RocksDB_MaxOpenFiles                 | Max number of files to be concurrently opened by RocksDB                                                                 |
| RocksDB_MaxTotalWalSize              | Max sized to be used by RocksDB's write-ahead-log                                                                        |
| RunAsNonRoot                         | If set, runs at user = 1000 instead of root                                                                              |
| RuntimeLogLevel                      | Runtime diagnostic logging level, **fatal / error / warning / info / debug / verbose**, defaults to **info**             |
| SendRuntimeQualityTelemetry          | Whether to enable sending runtime diagnostics metric, defaults to **true**                                               |
| Storage_LogLevel                     | RocksDB diagnostic log level, **NONE / FATAL / HEADER / ERROR / WARN / INFO / DEBUG**, defaults to **NONE**              |
| StorageClassName                     | StorageClassName to be used when creating a PersistedVolumeClaim                                                         |
| StorageFolder                        | Path to place the EdgeHub database directory, defaults to **TempPath** of the current OS                                 |
| UpstreamProtocol                     | Protocol used to for upstream connections, defaults to **Amqp**, and falls back to AmqpWs                                |
| UseMountSourceForVolumeName          | ???, defaults to **false**                                                                                               |
| UsePersistentStorage                 | Whether to save deployment config and module states to disk, defaults to **true**                                        |
| UseServerHeartbeat                   | Sets the client-side heartbeat interval to 60sec for the Agent's upstream AMQP connection, defaults to **true**          |
| EdgeDeviceHostName                   | omit?                                                                                                                    |
| experimentalFeatures                 | omit?                                                                                                                    |
| DockerUri                            | omit?                                                                                                                    |
| DeviceConnectionString               | omit?                                                                                                                    |
| IOTEDGE_MANAGEMENTURI                | omit?                                                                                                                    |
| IOTEDGE_WORKLOADURI                  | omit?                                                                                                                    |
| IOTEDGE_IOTHUBHOSTNAME               | omit?                                                                                                                    |
| IOTEDGE_GATEWAYHOSTNAME              | omit?                                                                                                                    |
| IOTEDGE_DEVICEID                     | omit?                                                                                                                    |
| IOTEDGE_MODULEID                     | omit?                                                                                                                    |
| IOTEDGE_APIVERSION                   | omit?                                                                                                                    |
| K8s proxy vars                       | omit?                                                                                                                    |
| K8s object owner vars                | omit?                                                                                                                    |
| DockerLoggingDriver                  | omit?                                                                                                                    |
| DockerLoggingOptions                 | omit?                                                                                                                    |
| DockerRegistryAuth                   | omit?                                                                                                                    |
| EnableStreams                        | obsolete?                                                                                                                |

## EdegHub
| Variable                                | Description                                                                                                               |
|-----------------------------------------|---------------------------------------------------------------------------------------------------------------------------|
| amqpSettings:enabled                    | Whether the AMQP protocol head should be enabled, defaults to **true**                                                        |
| amqpSettings:port                       | The port for the AMQP protocol head to listen on, defaults to **5671**                                                        |
| AuthenticationMode                      | Determines who performs authentication, Scope for EdgeHub, Cloud for IotHub, defaults to **CloudAndScope**                    |
| BackupFolder                            | Path to place the backup EdgeHub database directory, defaults to **TempPath** of the current OS                               |
| CacheTokens                             | Whether client authentication tokens are saved to disk, defaults to **false**                                                 |
| CheckEntireQueueOnCleanup               | Periodically check all pending messages for TTL expiry, incurs more I/O but saves more storage, defaults to **false**         |
| ClientCertAuthEnabled                   | Allows dev certificates to be used during SSL handshake with upstream and bypass cert validation, defaults to **false**       |
| CloseCloudConnectionOnDeviceDisconnect  | If a leaf device disconnections, immediately closes the corresponding upstream connection, defaults to **true**               |
| CloseCloudConnectionOnIdleTimeout       | Whether the upstream connection should be closed when CloudConnectionIdleTimeoutSecs is reached, defaults to **true**         |
| CloudConnectionIdleTimeoutSecs          | If there are no IoT operations, time span to wait before the upstream connection is considered idle, defaults to **3600**     |
| CloudOperationTimeoutSecs               | Time out for any upstream IoT operation, defaults to **20**                                                                   |
| ConfigRefreshFrequencySecs              | Interval at which the EdgeHub config is refreshed from upstream, defaults to **3600**                                         |
| configSource                            | Uses config from either EdgeHub twin, or a local config source, defaults to **twin**                                          |
| ConnectivityCheckFrequencySecs          | Interval at which EdgeHub will ping upstream to ensure connectivity is still present, defaults to **300**                     |
| DeviceScopeCacheRefreshRateSecs         | Interval at which leaf and module identities are refreshed from upstream, defaults to **3600**                                |
| EnableRoutingLogging                    | Whether message routing logs should be enabled, defaults to **false**                                                         |
| EncryptTwinStore                        | Whether to encrypt the twin data before persisting to disk, defaults to **true**                                              |
| https_proxy                             | Address of the proxy to use for outbound HTTPS requests                                                                       |
| httpSettings:enabled                    | Whether the HTTP server should be enabled, defaults to **true**                                                               |
| httpSettings:port                       | The port for the HTTP protocol head to listen on, defaults to **443**                                                         |
| IotHubConnectionPoolSize                | Pool size for upstream AMQP connection                                                                                        |
| MaxConnectedClients                     | Maximum number of downstream clients allowed to connect, defaults to **101** (100 clients + 1 EdgeHub)                        |
| MaxUpstreamBatchSize                    | Max number of messages to concurrently send upstream, defaults to **10**                                                      |
| MessageAckTimeoutSecs                   | Time span to wait for sending a message downstream to a leaf device, defaults to **30**                                       |
| metrics:listener:host                   | Hostname of the metrics listener, used to construct the metrics listener URL, defaults to **\***                              |
| metrics:listener:MetricsEnabled         | Whether to enable metrics listener, default to true                                                                           |
| metrics:listener:MetricsHistogramMaxAge | Time interval (in hours) for the metrics histogram, defaults to **1**                                                         |
| metrics:listener:port                   | Port of the metrics listener, used to construct the metrics listener URL, defaults to **9600**                                |
| metrics:listener:suffix                 | Appended to the metrics listener URL, defaults to **metrics**                                                                 |
| MinTwinSyncPeriodSecs                   | Maximum frequency for pull any device/module twin, defaults to **120**                                                        |
| mqttSettings:enabled                    | Whether the MQTT broker should be enabled, defaults to **true**                                                               |
| OptimizeForPerformance                  | Increase RocksDB file I/O usage to speed up message storage, defaults to **true**                                             |
| ReportedPropertiesSyncFrequencySecs     | Maximum frequency for pushing reported properties upstream, defaults to **5**                                                 |
| RocksDB_MaxOpenFiles                    | Max number of files to be concurrently opened by RocksDB                                                                      |
| RocksDB_MaxTotalWalSize                 | Max sized to be used by RocksDB's write-ahead-log                                                                             |
| RuntimeLogLevel                         | Runtime diagnostic logging level, **fatal / error / warning / info / debug / verbose**, defaults to **info**                  |
| ShutdownWaitPeriod                      | Seconds to wait on shutdown before hard termination, defaults to **60**                                                       |
| SslProtocols                            | TLS protocol(s) to be supported, tls1.0 / tls1.1 / tls1.2, or any combination thereof separated by comma, defaults to **all** |
| Storage_LogLevel                        | RocksDB diagnostic log level, **NONE / FATAL / HEADER / ERROR / WARN / INFO /DEBUG**, defaults to **NONE**                    |
| StorageFolder                           | Path to place the EdgeHub database directory, defaults to **TempPath** of the current OS                                      |
| UpstreamFanOutFactor                    | Max number of message groups to concurrently process for sending, grouped by sender, defaults to **10**                       |
| UpstreamProtocol                        | Protocol used to for upstream connections, defaults to **Amqp**, and falls back to AmqpWs                                     |
| UseServerHeartbeat                      | Sets the client-side heartbeat interval to 60sec for upstream AMQP connections, defaults to **true**                          |
| experimentalFeatures                    | omit?                                                                                                                         |
| IOTEDGE_WORKLOADURI                     | omit?                                                                                                                         |
| IOTEDGE_APIVERSION                      | omit?                                                                                                                         |
| IOTEDGE_MODULEGENERATIONID              | omit?                                                                                                                         |
| EdgeHubDevServerCertificateFile         | omit?                                                                                                                         |
| EdgeHubDevServerPrivateKeyFile          | omit?                                                                                                                         |
| EdgeHubDevTrustBundleFile               | omit?                                                                                                                         |
| EDGEDEVICEHOSTNAME                      | omit?                                                                                                                         |
| IOTEDGE_IOTHUBHOSTNAME                  | omit?                                                                                                                         |
| IOTEDGE_DEVICEID                        | omit?                                                                                                                         |
| IOTEDGE_MODULEID                        | omit?                                                                                                                         |
| IotHubConnectionString                  | omit?                                                                                                                         |
| usePersistentStorage                    | obsolete?                                                                                                                     |
| storeAndForwardEnabled                  | obsolete?                                                                                                                     |
| EnableNonPersistentStorageBackup        | obsolete?                                                                                                                     |
| TwinManagerVersion                      | obsolete?                                                                                                                     |