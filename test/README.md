# End-to-end tests

## Test code structure

There are three directories under `test/`:

1. `test/Microsoft.Azure.Devices.Edge.Test/`: The actual tests live here, as do "fixtures" (setup and tear-down code that runs before/after/between tests) and the `Context` class (exposes the test arguments to tests).
2. `test/Microsoft.Azure.Devices.Edge.Test.Common/`: The helper library tests use to do interesting things--like deploy modules or wait for a message to arrive in IoT Hub--lives here.
3. `test/modules`: All test modules live here. The common pattern for an end-to-end test is to deploy some modules that run through a scenario (i.e., the thing you actually want to test, like sending messages between modules), then wait for them to report the results.

## How to run the tests
*Note: The steps mentioned here have been run and validated on an ubuntu18.04/amd64 machine. If you are running a different os/arch, your steps may differ.*

### One time setup

#### Setting up your local machine

##### Prerequisites
To run the End-to-End tests, we will be building the required binaries from the code, build container images and push them to a local docker repository. The tests will install the IoT Edge runtime from the binaries on your machine and run the containers using your local repository.

It is important that your machine satisfies the requirements to run IoT Edge. See our installation docs ([Linux](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux), [Windows](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-windows)) for more information on prerequisites. In particular, make sure to prepare your machine to access the Microsoft installation pacakges. *The steps to do this are listed below for your convenience. Again, please look at the section in the linked document for your os/arch combination if it is different from ubuntu18.04/amd64*
~~~sh
# Setup the repository information
curl https://packages.microsoft.com/config/ubuntu/18.04/multiarch/prod.list > ./microsoft-prod.list

# Setup the Microsoft GPG pulic key in the apt's trusted list.
sudo cp ./microsoft-prod.list /etc/apt/sources.list.d/
curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg
sudo cp ./microsoft.gpg /etc/apt/trusted.gpg.d/

# Run update to update the package lists on your machine
sudo apt-get update
~~~

Also, create a rootCA certificate for your machine *(See [Create Certs](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-create-test-certificates?view=iotedge-2020-11#create-root-ca-certificate) for more details)*. Navigate to the directory where the certificate generation scripts are found (typically iotedge/tools/CACertificates) and run
~~~ sh
./certGen.sh create_root_and_intermediate

# This will create root and intermediate CA certs. The Root CA cert will be located in the 
# certs sub directory and will be needed to run the tests.
~~~

##### Install java
The build scripts use Java for code generation tasks, so you will need a jdk on your machine.
Here is how you can install the openjdk.
~~~ sh
sudo apt-get install openjdk-11-jdk
~~~

##### Configure your local container repository
We will be building the container images from the codebase and pushing them to  local docker repository. To prepare for this, we will install docker and configure the docker repository using the steps below. See [Deploy a registry server](https://docs.docker.com/registry/deploying/) for further information regarding these steps to configure the registry.

###### Install docker
See [Install docker using the convenience scripts](https://docs.docker.com/engine/install/ubuntu/#install-using-the-convenience-script) for details

~~~ sh
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh 
~~~
###### Create certificates
We will be running the docker registry with TLS and authentication turned on. If you already have certs for TLS, feel free to use those and skip the cert creation steps listed belwo. 
In a convenient directory, create a folder called auth to store your keys and certs (*I have used /home/azureuser/auth*)
~~~sh
mkdir auth
cd auth
# Create a private key
openssl genrsa -out server.key  2048
# Create a csr
openssl req -new -key server.key -out server.csr
# Create a cert from the csr
openssl x509 -req -days 365 -in server.csr -signkey server.key -out server.crt

# At the end of these steps, you have 3 files in your auth directory - server.key (the  
# key), server.csr (the certificate signing request) and, server.crt (the certificate)
~~~

###### Setup native basic auth
~~~ sh
sudo apt-get install apache2-utils
# Create password file
: | sudo tee htpasswd
# Create username and password. Replace the username and password strings 
# below with a user/password combo that you like
echo "password" | sudo htpasswd -iB htpasswd username
~~~

###### Configure and run the local docker repository
~~~ sh
# Set up the docker registry to listen on port 5000 and to restart automatically.
sudo docker run -d  -p 5000:5000 \
  --name registry  \
  -v /home/azureuser/auth/:/etc/security \
  -e REGISTRY_HTTP_TLS_CERTIFICATE=/etc/security/server.crt  \
  -e REGISTRY_HTTP_TLS_KEY=/etc/security/server.key \
  -e REGISTRY_AUTH=htpasswd   \
  -e REGISTRY_AUTH_HTPASSWD_PATH=/etc/security/htpasswd   \
  -e REGISTRY_AUTH_HTPASSWD_REALM="Registry Realm"  \
  --restart always  \
  registry:2

# Run docker ps to validate that the registry came up successfully
sudo docker ps
# If all goes well, you will see the something like below when you run Docker ps.
# 59867fe47a5b   registry:2   "/entrypoint.sh /etc…"   6 seconds ago   Up 6 seconds   0.0.0.0:5000->5000/tcp, :::5000->5000/tcp   registry

# Run docker login to validate setup. This step will cache your credentials on the host
# and will be required later when you build and push docker images
~~~

##### Install .NET
End-to-end tests are written in .NET Core and run with the `dotnet test` command, so you need to [install .NET Core SDK](https://docs.microsoft.com/en-us/dotnet/core/install/sdk). *Note: You will have to install .NET version 3.1*

#### Cloud side setup for the tests
##### IoT Hub
If you don't already have an existing IoTHub, create one. There is no special configuration required, except for making sure that your IoTHub is enabled for public access.

##### Device Provisioning Service (DPS)
Create a DPS instance. A subset of the end to end tests will use DPS for testing device provisioning scenarios. 
###### Create enrollment group for symmetric key based enrollment
In your DPS instance, create new enrollment group. For this enrollment group, set the attestation type to be symmetric key, toggle IoT Edge Device to true, select your iothub by clicking on Link a new Iot Hub, and select the access policy to be iotHubOwner
		
###### Create enrollment group for X.509 certificate based enrollment
In your DPS instance, create another enrollment group for X.509 certificate based enrollment. Link this new group to your IotHub, associate it with the your machine's root CA certificate and set the IotEdge Device toggle to true.

##### Create a storage account/container
Create a storage container, with access level set to private. Create a SAS token URL ( allow https only, permissions set to "write" only). Note down this URL as you will need it to set the enviornment.

### Building the code
#### Building the binaries
From the top folder of the codebase ( i.e., the iotedge folder), run the following(*Note: Some of these steps will require to be run as sudo*)
~~~ sh
# run build branch
scripts/linux/buildBranch
# Build mqttd
scripts/linux/cross-platform-rust-build.sh --os alpine --arch amd64 --build-path mqtt/mqttd
# Build watchdog
scripts/linux/cross-platform-rust-build.sh --os alpine --arch amd64 --build-path edge-hub/watchdog

# setup environment variables
export PACKAGE_OS="ubuntu18.04"
export PACKAGE_ARCH="amd64"

# Package the binary
./edgelet/build/linux/package.sh

# This will create the installable packages. It will typically be located
# in the ./edgelet/target/release/ and named something like aziot-edge_1.2.3-1_amd64.deb
~~~

#### Building the container images and pushing them to the local docker repo
From the top folder of the codebase, run the following. 
~~~ sh
# Consolidate artifacts for edge-hub
sudo scripts/linux/consolidate-build-artifacts.sh --artifact-name "edge-hub"

# Build and push images to local repo
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "edge-hub" -P "edge-hub"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "edge-agent" -P "Microsoft.Azure.Devices.Edge.Agent.Service"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "load-gen" -P "load-gen"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "direct-method-sender" -P "DirectMethodSender"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "direct-method-receiver" -P "DirectMethodReceiver"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "network-controller" -P "NetworkController"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "relayer" -P "Relayer"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "temperature-filter" -P "TemperatureFilter"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "simulated-temperature-sensor" -P "SimulatedTemperatureSensor"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "test-result-coordinator" -P "TestResultCoordinator"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "number-logger" -P "NumberLogger"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "diagnostics" -P "IotedgeDiagnosticsDotnet"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "metrics-validator" -P "MetricsValidator"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "temperature-filter-function" -P "EdgeHubTriggerCSharp"  -v "latest" --bin-dir target

# The Generic MQTT tester image follows a slightly different build pattern. Use the following
# steps to build and push those to docker.
sudo scripts/linux/cross-platform-rust-build.sh --os ubuntu18.04 --arch amd64 --build-path test/modules/generic-mqtt-tester/
cd test/modules/generic-mqtt-tester
sudo docker build --no-cache -t localhost:5000/microsoft/generic-mqtt-tester:latest-linux-amd64 --file docker/linux/amd64/Dockerfile --build-arg EXE_DIR=. target
sudo docker push localhost:5000/microsoft/generic-mqtt-tester:latest-linux-amd64
~~~

### Running the tests

#### Test parameters

The end-to-end tests take several parameters, which they expect to find in a file named `context.json` in the same directory as the test binaries (e.g., `test/Microsoft.Azure.Devices.Edge.Test/bin/Debug/netcoreapp2.1/context.json`). Parameter names are case-insensitive. The parameters are:

| Name | Required | Description |
|------|----------|-------------|
| `caCertScriptPath` | * | Path to the folder containing `certGen.sh` (Linux) or `ca-certs.ps1` (Windows). Use when running the test 'TransparentGateway', ignored otherwise. |
| `dpsIdScope` | * | The [ID Scope](https://docs.microsoft.com/en-us/azure/iot-dps/concepts-device#id-scope) assigned to a Device Provisioning Service. Used when running any DPS tests, ignored otherwise. |
| `edgeAgentImage` || Docker image to pull/use for Edge Agent. If not given, the default value `mcr.microsoft.com/azureiotedge-agent:1.0` is used. This setting only applies to any configurations deployed by the tests. Note also that the default value is ALWAYS used in config.yaml to start IoT Edge; this setting only applies to any configurations deployed by the tests. |
| `edgeHubImage` || Docker image to pull/use for Edge Hub. If not given, `mcr.microsoft.com/azureiotedge-hub:1.0` is used. |
| `installerPath` || Path to the Windows installer script `IotEdgeSecurityDaemon.ps1`. This parameter is ignored on Linux, and optional on Windows. If not given on Windows, the default script will be downloaded from https://aka.ms/iotedge-win to a temporary location. |
| `loadGenImage` | * | LoadGen image to be used. Required when running PriorityQueue tests, ignored otherwise.|
| `logFile` || Path to which all test output will be written, including verbose output. This setting allows the user to capture all the details of a test pass while keeping the shell window output free of visual clutter. Note that daemon logs and module logs are always written to the same directory as the test binaries (e.g., `test/Microsoft.Azure.Devices.Edge.Test/bin/Debug/netcoreapp2.1/*.log`), independent of this parameter. |
| `methodReceiverImage` | * | Docker image to pull/use for the 'DirectMethodReceiver' module. Required when running the test 'ModuleToModuleDirectMethod', ignored otherwise. |
| `methodSenderImage` | * | Docker image to pull/use for the 'DirectMethodSender' module. Required when running the test 'ModuleToModuleDirectMethod', ignored otherwise. |
| `networkControllerImage` | * | NetworkControllerImage image to be used. Required when running PriorityQueue tests, ignored otherwise.|
| `optimizeForPerformance` || Boolean value passed to Edge Hub. Usually set to 'false' when running on more constrained platforms like Raspberry Pi. If not given, it defaults to 'true'. |
| `packagePath` || Path to the folder containing IoT Edge installation packages (e.g., .deb files on Linux, .cab file on Windows). If not given, the latest stable release packages are downloaded and used. |
| `parentHostname` || parent hostname to enable connection to parent edge device in nested edge scenario. Required when running nested edge tests, ignored otherwise. |
| `proxy` || URL of an HTTPS proxy server to use for all communication to IoT Hub. |
| `registries` || JSON array of container registries to be used by the tests. This information will be added to configurations deployed to the edge device under test. If not given, IoT Edge will attempt anonymous access to container registries. The format of each JSON object in the array is: `{ "address": "{server hostname}", "username": "{username}" }`. Note that each object must also have a value `"password": "{password}"`, but you are encouraged to use an environment variable to meet this requirement (see _Secret test parameters_ below). |
| `relayerImage` | * | Relayer image to be used. Required when running PriorityQueue tests, ignored otherwise.|
| `rootCaCertificatePath` | * | Full path to a root certificate given to leaf test devices so they can verify the authenticity of Edge Hub during the TLS handshake. Required when running the test 'TransparentGateway', ignored otherwise. |
| `rootCaPrivateKeyPath` | * | Full path to a file containing the private key associated with `rootCaCertificatePath`. Required when running the test 'TransparentGateway', ignored otherwise. |
| `setupTimeoutMinutes` || The maximum amount of time, in minutes, test setup should take. This includes setup for all tests, for the tests in a fixture, or for a single test. If this time is exceeded, the associated test(s) will fail with a timeout error. If not given, the default value is `5`. |
| `teardownTimeoutMinutes` || The maximum amount of time, in minutes, test teardown should take. This includes teardown for all tests, for the tests in a fixture, or for a single test. If this time is exceeded, the associated test(s) will fail with a timeout error. If not given, the default value is `2`. |
| `tempFilterFuncImage` | * | Azure temperature filter function to be used. Required when running the test 'TempFilterFunc', ignored otherwise.|
| `tempFilterImage` | * | Docker image to pull/use for the temperature filter module. Required when running the test 'TempFilter', ignored otherwise.|
| `tempSensorImage` || Docker image to pull/use for the temperature sensor module (see the test 'TempSensor'). If not given, `mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0` is used.|
| `numberLoggerImage` || Docker image to pull/use for the Edge agent direct method tests. Used to generate predictable logs. |
| `testResultCoordinatorImage` | * | TestResultCoordinator image to be used. Required when running PriorityQueue or GenericMqtt tests, ignored otherwise.|
| `genericMqttTesterImage` | * | GenericMqttTester image to be used. Required when running GenericMqtt tests, ignored otherwise.
| `testTimeoutMinutes` || The maximum amount of time, in minutes, a single test should take. If this time is exceeded, the associated test will fail with a timeout error. If not given, the default value is `5`. |
| `verbose` || Boolean value indicating whether to output more verbose logging information to standard output during a test run. If not given, the default is `false`. |
#### Sample context.json
~~~ json
{
"packagePath": "/home/azureuser/iotedge/edgelet/target/release/",
"edgeAgentImage": "localhost:5000/microsoft/edge-agent:latest-linux-amd64",
"edgeHubImage": "localhost:5000/microsoft/edge-hub:latest-linux-amd64",
"loadGenImage": "localhost:5000/microsoft/load-gen:latest-linux-amd64",
"methodSenderImage": "localhost:5000/microsoft/direct-method-sender:latest-linux-amd64",
"methodReceiverImage": "localhost:5000/microsoft/direct-method-receiver:latest-linux-amd64",
"networkControllerImage": "localhost:5000/microsoft/network-controller:latest-linux-amd64",
"relayerImage": "localhost:5000/microsoft/relayer:latest-linux-amd64",
"tempFilterFuncImage": "localhost:5000/microsoft/temperature-filter-function:latest-linux-amd64",
"tempFilterImage": "localhost:5000/microsoft/temperature-filter:latest-linux-amd64",
"tempSensorImage": "localhost:5000/microsoft/simulated-temperature-sensor:latest-linux-amd64",
"testResultCoordinatorImage": "localhost:5000/microsoft/test-result-coordinator:latest-linux-amd64",
"numberLoggerImage": "localhost:5000/microsoft/number-logger:latest-linux-amd64",
"genericMqttTesterImage": "localhost:5000/microsoft/generic-mqtt-tester:latest-linux-amd64",
"diagnosticsImage": "localhost:5000/microsoft/diagnostics:latest-linux-amd64",
"metricsValidatorImage": "localhost:5000/microsoft/metrics-validator:latest-linux-amd64",
"caCertScriptPath": "/home/azureuser/iotedge/test/certs",
"dpsIdScope": "0ne003A5268",
"rootCaCertificatePath": "/home/azureuser/iotedge/test/certs/certs/azure-iot-test-only.root.ca.cert.pem",
"rootCaPrivateKeyPath": "/home/azureuser/iotedge/test/certs/private/azure-iot-test-only.root.ca.key.pem",
"verbose": "true",
"logFile": "/home/azureuser/iotedge/logs/logfile",
"registries":
        [
                {
                        "address": "localhost:5000",
                        "username": "username"
                }
        ]
}
~~~

### Test secrets

The tests also expect to find several _secret_ parameters. While these can technically be added to `context.json`, it is recommended that you create environment variables and make them available to the test framework in a way that avoids committing them to your shell's command history or saving them in clear text on your filesystem. When set as environment variables, all secret parameters must be prefixed with `E2E_`. Parameter names are case-**in**sensitive; they're only shown in uppercase here to follow the common convention for environment variables, and to stand out as secrets.

| Name | Required | Description |
|------|----------|-------------|
| `E2E_DPS_GROUP_KEY` | * | The symmetric key of the DPS [enrollment group](https://docs.microsoft.com/en-us/azure/iot-dps/concepts-service#enrollment-group) to use. Used when running any DPS tests, ignored otherwise. |
| `E2E_IOT_HUB_CONNECTION_STRING` | ✔ | Hub-scoped IoT Hub connection string that can be used to get/add/remove devices, deploy edge configurations, and get/update module twins. |
| `E2E_EVENT_HUB_ENDPOINT` | ✔ | Connection string used to connect to the Event Hub-compatible endpoint of your IoT Hub, to listen for D2C events sent by devices or modules. |
| `E2E_REGISTRIES__{n}__PASSWORD` || Password associated with a container registry entry in the `registries` array of `context.json`. `{n}` is the number corresponding to the (zero-based) array entry. For example, if you specified a single container registry in the `registries` array, the corresponding parameter would be `[E2E_]REGISTRIES__0__PASSWORD`. |
| `E2E_ROOT_CA_PASSWORD` || The password associated with the root certificate specified in `rootCaCertificatePath`. |
| `E2E_BLOB_STORE_SAS` || The sas token used to upload module logs and support bundle in the tests. |

_Note: the definitive source for information about test parameters is `test/Microsoft.Azure.Devices.Edge.Test/helpers/Context.cs`._

#### Sample environnment settings
~~~ sh
export E2E_IOT_HUB_CONNECTION_STRING="HostName=..."
export E2E_EVENT_HUB_ENDPOINT="Endpoint..."
export E2E_DPS_GROUP_KEY="IGcMo..."
export DPS_GROUP_KEY="IGcMoH..."
export E2E_REGISTRIES__0__PASSWORD="password"
export E2E_BLOB_STORE_SAS="https://...."
~~~

### Run the tests

With the test parameters and secrets in place, you can run all the end-to-end tests from the command line. From the top folder of the codebase

```bash
cd test
sudo --preserve-env dotnet test ./Microsoft.Azure.Devices.Edge.Test \
     --filter "TestCategory=EndToEnd&TestCategory!=NestedEdgeOnly"
```

To learn about other ways to run the tests (e.g., to run only certain tests), see 
[Running selective unit tests](https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests#nunit)

### Troubleshooting

#### File handle limit in VS Code

If you are using VS Code in Linux, it is recommended that you increase the maximum number of file I/O handles to prevent tests from hitting the limit. To learn how to increase the file I/O capacity, see
https://code.visualstudio.com/docs/setup/linux#_visual-studio-code-is-unable-to-watch-for-file-changes-in-this-large-workspace-error-enospc.

_Note: You will need to increase both `fs.inotify.max_user_instances` and `fs.inotify.max_user_watches`_
