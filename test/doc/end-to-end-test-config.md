# End to End Test Parameters

_Note: A ✔ in the required column indicates that it is required for all the tests, where as * indicates that the attribute is only required for a subset of the tests_ 

| Name | Required | Description |
|------|----------|-------------|
| `caCertScriptPath` | * | Path to the folder containing `certGen.sh` (Linux) or `ca-certs.ps1` (Windows). Required when running the test 'TransparentGateway', ignored otherwise. |
| `dpsIdScope` | * | The [ID Scope](https://docs.microsoft.com/en-us/azure/iot-dps/concepts-device#id-scope) assigned to a Device Provisioning Service. Required when running any DPS tests, ignored otherwise. |
| `edgeAgentImage` || Docker image to pull/use for Edge Agent. If not given, the default value `mcr.microsoft.com/azureiotedge-agent:1.0` is used. This setting only applies to any configurations deployed by the tests. Note also that the default value is ALWAYS used in config.yaml to start IoT Edge; this setting only applies to any configurations deployed by the tests. |
| `edgeHubImage` || Docker image to pull/use for Edge Hub. If not given, `mcr.microsoft.com/azureiotedge-hub:1.0` is used. |
| `installerPath` || Path to the Windows installer script `IotEdgeSecurityDaemon.ps1`. This parameter is ignored on Linux, and optional on Windows. If not given on Windows, the default script will be downloaded from https://aka.ms/iotedge-win to a temporary location. |
| `logFile` || Path to which all test output will be written, including verbose output. This setting allows the user to capture all the details of a test pass while keeping the shell window output free of visual clutter. Note that daemon logs and module logs are always written to the same directory as the test binaries (e.g., `test/Microsoft.Azure.Devices.Edge.Test/bin/Debug/netcoreapp2.1/*.log`), independent of this parameter. |
| `methodReceiverImage` | * | Docker image to pull/use for the 'DirectMethodReceiver' module. Required when running the test 'ModuleToModuleDirectMethod', ignored otherwise. |
| `methodSenderImage` | * | Docker image to pull/use for the 'DirectMethodSender' module. Required when running the test 'ModuleToModuleDirectMethod', ignored otherwise. |
| `optimizeForPerformance` || Boolean value passed to Edge Hub. Usually set to 'false' when running on more constrained platforms like Raspberry Pi. If not given, it defaults to 'true'. |
| `packagePath` || Path to the folder containing IoT Edge installation packages (e.g., .deb files on Linux, .cab file on Windows). If not given, the latest stable release packages are downloaded and used. |
| `proxy` || URL of an HTTPS proxy server to use for all communication to IoT Hub. |
| `registries` || JSON array of container registries to be used by the tests. This information will be added to configurations deployed to the edge device under test. If not given, IoT Edge will attempt anonymous access to container registries. The format of each JSON object in the array is: `{ "address": "{server hostname}", "username": "{username}" }`. Note that each object must also have a value `"password": "{password}"`, but you are encouraged to use an environment variable to meet this requirement (see _Secret test parameters_ below). |
| `rootCaCertificatePath` | * | Full path to a root certificate given to leaf test devices so they can verify the authenticity of Edge Hub during the TLS handshake. Required when running the test 'TransparentGateway', ignored otherwise. |
| `rootCaPrivateKeyPath` | * | Full path to a file containing the private key associated with `rootCaCertificatePath`. Required when running the test 'TransparentGateway', ignored otherwise. |
| `setupTimeoutMinutes` || The maximum amount of time, in minutes, test setup should take. This includes setup for all tests, for the tests in a fixture, or for a single test. If this time is exceeded, the associated test(s) will fail with a timeout error. If not given, the default value is `5`. |
| `teardownTimeoutMinutes` || The maximum amount of time, in minutes, test teardown should take. This includes teardown for all tests, for the tests in a fixture, or for a single test. If this time is exceeded, the associated test(s) will fail with a timeout error. If not given, the default value is `2`. |
| `testResultCoordinatorImage` | * | TestResultCoordinator image to be used. Required when running PriorityQueue tests, ignored otherwise.|
| `networkControllerImage` | * | NetworkControllerImage image to be used. Required when running PriorityQueue tests, ignored otherwise.|
| `loadGenImage` | * | LoadGen image to be used. Required when running PriorityQueue tests, ignored otherwise.|
| `relayerImage` | * | Relayer image to be used. Required when running PriorityQueue tests, ignored otherwise.|
| `tempFilterFuncImage` | * | Azure temperature filter function to be used. Required when running the test 'TempFilterFunc', ignored otherwise.|
| `tempFilterImage` | * | Docker image to pull/use for the temperature filter module. Required when running the test 'TempFilter', ignored otherwise.|
| `metricsCollectorImage` | * | Docker image to pull/use for the Metrics Collector module. Required when running the test 'MetricsCollector', ignored otherwise.|
| `tempSensorImage` || Docker image to pull/use for the temperature sensor module (see the test 'TempSensor'). If not given, `mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0` is used.|
| `edgeAgentBootstrapImage` || Docker image to pull/use for the initial startup of edgeAgent. It is the EdgeAgent image used in config.yaml. This is a temporary parameter - only here now because the 1.0 version is incompatible with the way this test framework verifies deployments in the master branch. If you want to specify your own, the container registry used is the first registry in the list of registries given by the 'registries' parameter|'
| `numberLoggerImage` || Docker image to pull/use for the Edge agent direct method tests. Used to generate predictable logs. |
| `testTimeoutMinutes` || The maximum amount of time, in minutes, a single test should take. If this time is exceeded, the associated test will fail with a timeout error. If not given, the default value is `5`. |
| `hubResourceId` | * | Full path to Iot Hub that will receive the metrics messages in the following format - `/resource/subscriptions/<Azure subscription GUID>/resourceGroups/<resource group name>/providers/Microsoft.Devices/IotHubs/<Iot Hub name>`. Required when running the test 'MetricsCollector', ignored otherwise.|
| `verbose` || Boolean value indicating whether to output more verbose logging information to standard output during a test run. If not given, the default is `false`. |

# [Environment Variables](#environment-variables)

| Name | Required | Description |
|------|----------|-------------|
| `E2E_DPS_GROUP_KEY` | * | The symmetric key of the DPS [enrollment group](https://docs.microsoft.com/en-us/azure/iot-dps/concepts-service#enrollment-group) to use. Required when running any DPS tests, ignored otherwise. |
| `E2E_IOT_HUB_CONNECTION_STRING` | ✔ | Hub-scoped IoT Hub connection string that can be used to get/add/remove devices, deploy edge configurations, and get/update module twins. |
| `E2E_EVENT_HUB_ENDPOINT` | ✔ | Connection string used to connect to the Event Hub-compatible endpoint of your IoT Hub, to listen for D2C events sent by devices or modules. |
| `E2E_REGISTRIES__{n}__PASSWORD` || Password associated with a container registry entry in the `registries` array of `context.json`. `{n}` is the number corresponding to the (zero-based) array entry. For example, if you specified a single container registry in the `registries` array, the corresponding parameter would be `E2E_REGISTRIES__0__PASSWORD`. |
| `E2E_ROOT_CA_PASSWORD` || The password associated with the root certificate specified in `rootCaCertificatePath`. |
| `E2E_BLOB_STORE_SAS` || The sas token used to upload module logs and support bundle in the tests. |

_Note: the definitive source for information about test parameters is `test/Microsoft.Azure.Devices.Edge.Test/helpers/Context.cs`._