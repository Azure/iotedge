# How to run the end-to-end tests

## Software Dependency
Since an IoT Edge device is responsible for running end-to-end (E2E) tests; hence,
the software requirements to run a test is the combination of IoT Edge and test build.
* For Building Tests
    * .NET Core SDK

## Test parameters

The end-to-end tests take several parameters, which they expect to find in a file named `context.json` in the same directory as the test binaries (e.g., `test/Microsoft.Azure.Devices.Edge.Test/bin/Debug/netcoreapp2.1/context.json`). Parameter names are case-insensitive. The parameters are:

| Name | Required | Description |
|------|----------|-------------|
| `caCertScriptPath` | * | Path to the folder containing `certGen.sh` (Linux) or `ca-certs.ps1` (Windows). Required when running the test 'TransparentGateway', ignored otherwise. |
| `deviceId` || Name by which the edge device-under-test will be registered in IoT Hub. If not given, a name will automatically be generated in the format: `e2e-{device hostname}-{yyMMdd-HHmmss.fff}`. DPS tests will derive their "registration ID" from `deviceId` by appending the normalized test name (lowercased, with sequences of non-word characters replaced by a single dash). |
| `dpsIdScope` | * | The [ID Scope](https://docs.microsoft.com/en-us/azure/iot-dps/concepts-device#id-scope) assigned to a Device Provisioning Service. Required when running any DPS tests, ignored otherwise. |
| `edgeAgentImage` || Docker image to pull/use for Edge Agent. If not given, the default value `mcr.microsoft.com/azureiotedge-agent:1.0` is used. Note also that the default value is ALWAYS used in config.yaml to start IoT Edge; this setting only applies to any configurations deployed by the tests. |
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
| `tempFilterFuncImage` | * | Azure temperature filter function to be used. Required when running the test 'TempFilterFunc', ignored otherwise.|
| `tempFilterImage` | * | Docker image to pull/use for the temperature filter module. Required when running the test 'TempFilter', ignored otherwise.|
| `tempSensorImage` || Docker image to pull/use for the temperature sensor module (see the test 'TempSensor'). If not given, `mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0` is used.|
| `testTimeoutMinutes` || The maximum amount of time, in minutes, a single test should take. If this time is exceeded, the associated test will fail with a timeout error. If not given, the default value is `5`. |
| `verbose` || Boolean value indicating whether to output more verbose logging information to standard output during a test run. If not given, the default is `false`. |

## Test secrets

The tests also expect to find several _secret_ parameters. While these can technically be added to `context.json`, it is recommended that you create environment variables and make them available to the test framework in a way that avoids committing them to your shell's command history or saving them in clear text on your filesystem. When set as environment variables, all secret parameters must be prefixed with `E2E_`. Parameter names are case-insensitive; they're only shown in uppercase here so they follow the common convention for environment variables, and to stand out as secrets.

| Name | Required | Description |
|------|----------|-------------|
| `[E2E_]DPS_GROUP_KEY` | * | The symmetric key of the DPS [enrollment group](https://docs.microsoft.com/en-us/azure/iot-dps/concepts-service#enrollment-group) to use. Required when running any DPS tests, ignored otherwise. |
| `[E2E_]IOT_HUB_CONNECTION_STRING` | ✔ | Hub-scoped IoT Hub connection string that can be used to get/add/remove devices, deploy edge configurations, and get/update module twins. |
| `[E2E_]EVENT_HUB_ENDPOINT` | ✔ | Connection string used to connect to the Event Hub-compatible endpoint of your IoT Hub, to listen for D2C events sent by devices or modules. |
| `[E2E_]REGISTRIES__{n}__PASSWORD` || Password associated with a container registry entry in the `registries` array of `context.json`. `{n}` is the number corresponding to the (zero-based) array entry. For example, if you specified a single container registry in the `registries` array, the corresponding parameter would be `[E2E_]REGISTRIES__0__PASSWORD`. |
| `[E2E_]ROOT_CA_PASSWORD` || The password associated with the root certificate specified in `rootCaCertificatePath`. |

_Note: the definitive source for information about test parameters is `test/Microsoft.Azure.Devices.Edge.Test/helpers/Context.cs`._

## Running the tests

With the test parameters and secrets in place, you can run all the end-to-end tests from the command line

_In Windows Command Prompt (Admin),_

```cmd
cd {repo root}
dotnet test test/Microsoft.Azure.Devices.Edge.Test
```
_For Linux,_
```bash
cd {repo root}
sudo --preserve-env dotnet test ./test/Microsoft.Azure.Devices.Edge.Test 
```

For more details please refer to NUnit document: 
https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests#nunit

## Troubleshooting

If you are using a VSCode in Linux, it is recommend that you increase the maximum number of file I/O to prevent a test from erroring out by hitting the limit. To increase the file I/O capacity:
https://code.visualstudio.com/docs/setup/linux#_visual-studio-code-is-unable-to-watch-for-file-changes-in-this-large-workspace-error-enospc

