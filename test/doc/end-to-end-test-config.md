#End to End Test Parameters

| Name | Required | Description |
|------|----------|-------------|
| `caCertScriptPath` | * | Path to the folder containing `certGen.sh` (Linux) or `ca-certs.ps1` (Windows). Use when running the test 'TransparentGateway', ignored otherwise. |
| `dpsIdScope` | * | The [ID Scope](https://docs.microsoft.com/en-us/azure/iot-dps/concepts-device#id-scope) assigned to a Device Provisioning Service. Used when running any DPS tests, ignored otherwise. |
| `edgeAgentImage` || Docker image to pull/use for Edge Agent. If not given, the default value `mcr.microsoft.com/azureiotedge-agent:1.0` is used. This setting only applies to any configurations deployed by the tests. Note also that the default value is ALWAYS used in config.yaml to start IoT Edge; this setting only applies to any configurations deployed by the tests. |
| `edgeHubImage` || Docker image to pull/use for Edge Hub. If not given, `mcr.microsoft.com/azureiotedge-hub:1.0` is used. |
| `installerPath` || Path to the Windows installer script `IotEdgeSecurityDaemon.ps1`. This parameter is ignored on Linux, and optional on Windows. If not given on Windows, the default script will be downloaded from https://aka.ms/iotedge-win to a temporary location. |
| `loadGenImage` | * | LoadGen image to be used. Used when running PriorityQueue tests, ignored otherwise.|
| `logFile` || Path to which all test output will be written, including verbose output. This setting allows the user to capture all the details of a test pass while keeping the shell window output free of visual clutter. Note that daemon logs and module logs are always written to the same directory as the test binaries (e.g., `test/Microsoft.Azure.Devices.Edge.Test/bin/Debug/netcoreapp2.1/*.log`), independent of this parameter. |
| `methodReceiverImage` | * | Docker image to pull/use for the 'DirectMethodReceiver' module. Used when running the test 'ModuleToModuleDirectMethod', ignored otherwise. |
| `methodSenderImage` | * | Docker image to pull/use for the 'DirectMethodSender' module. Used when running the test 'ModuleToModuleDirectMethod', ignored otherwise. |
| `networkControllerImage` | * | NetworkControllerImage image to be used. Used when running PriorityQueue tests, ignored otherwise.|
| `optimizeForPerformance` || Boolean value passed to Edge Hub. Usually set to 'false' when running on more constrained platforms like Raspberry Pi. If not given, it defaults to 'true'. |
| `packagePath` || Path to the folder containing IoT Edge installation packages (e.g., .deb files on Linux, .cab file on Windows). If not given, the latest stable release packages are downloaded and used. |
| `parentHostname` || parent hostname to enable connection to parent edge device in nested edge scenario. Used when running nested edge tests, ignored otherwise. |
| `proxy` || URL of an HTTPS proxy server to use for all communication to IoT hub. |
| `registries` || JSON array of container registries to be used by the tests. This information will be added to configurations deployed to the edge device under test. If not given, IoT Edge will attempt anonymous access to container registries. The format of each JSON object in the array is: `{ "address": "{server hostname}", "username": "{username}" }`. Note that each object must also have a value `"password": "{password}"`, but you are encouraged to use an environment variable to meet this requirement (see _Secret test parameters_ below). |
| `relayerImage` | * | Relayer image to be used. Used when running PriorityQueue tests, ignored otherwise.|
| `rootCaCertificatePath` | * | Full path to a root certificate given to leaf test devices so they can verify the authenticity of Edge Hub during the TLS handshake. Used when running the test 'TransparentGateway', ignored otherwise. |
| `rootCaPrivateKeyPath` | * | Full path to a file containing the private key associated with `rootCaCertificatePath`. Used when running the test 'TransparentGateway', ignored otherwise. |
| `setupTimeoutMinutes` || The maximum amount of time, in minutes, test setup should take. This includes setup for all tests, for the tests in a fixture, or for a single test. If this time is exceeded, the associated test(s) will fail with a timeout error. If not given, the default value is `5`. |
| `teardownTimeoutMinutes` || The maximum amount of time, in minutes, test teardown should take. This includes teardown for all tests, for the tests in a fixture, or for a single test. If this time is exceeded, the associated test(s) will fail with a timeout error. If not given, the default value is `2`. |
| `tempFilterFuncImage` | * | Azure temperature filter function to be used. Used when running the test 'TempFilterFunc', ignored otherwise.|
| `tempFilterImage` | * | Docker image to pull/use for the temperature filter module. Used when running the test 'TempFilter', ignored otherwise.|
| `tempSensorImage` || Docker image to pull/use for the temperature sensor module (see the test 'TempSensor'). If not given, `mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0` is used.|
| `numberLoggerImage` || Docker image to pull/use for the Edge agent direct method tests. Used to generate predictable logs. |
| `testResultCoordinatorImage` | * | TestResultCoordinator image to be used. Used when running PriorityQueue or GenericMqtt tests, ignored otherwise.|
| `genericMqttTesterImage` | * | GenericMqttTester image to be used. Used when running GenericMqtt tests, ignored otherwise.
| `testTimeoutMinutes` || The maximum amount of time, in minutes, a single test should take. If this time is exceeded, the associated test will fail with a timeout error. If not given, the default value is `5`. |
| `verbose` || Boolean value indicating whether to output more verbose logging information to standard output during a test run. If not given, the default is `false`. |

# [Environment Variables](#environment-variables)

| Name | Required | Description |
|------|----------|-------------|
| `E2E_DPS_GROUP_KEY` | ✔ | The symmetric key of the DPS [enrollment group](https://docs.microsoft.com/en-us/azure/iot-dps/concepts-service#enrollment-group) to use. Used when running any DPS tests, ignored otherwise. |
| `E2E_IOT_HUB_CONNECTION_STRING` | ✔ | Hub-scoped IoT hub connection string that can be used to get/add/remove devices, deploy edge configurations, and get/update module twins. |
| `E2E_EVENT_HUB_ENDPOINT` | ✔ | Connection string used to connect to the Event Hub-compatible endpoint of your IoT hub, to listen for D2C events sent by devices or modules. |
| `E2E_REGISTRIES__{n}__PASSWORD` || Password associated with a container registry entry in the `registries` array of `context.json`. `{n}` is the number corresponding to the (zero-based) array entry. For example, if you specified a single container registry in the `registries` array, the corresponding parameter would be `[E2E_]REGISTRIES__0__PASSWORD`. |
| `E2E_ROOT_CA_PASSWORD` || The password associated with the root certificate specified in `rootCaCertificatePath`. |
| `E2E_BLOB_STORE_SAS` || The sas token used to upload module logs and support bundle in the tests. |

_Note: the definitive source for information about test parameters is `test/Microsoft.Azure.Devices.Edge.Test/helpers/Context.cs`._
