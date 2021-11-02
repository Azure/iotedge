# End-to-end tests

## Test code structure

There are six directories under the `test/` directory, three of which are of immediate concern, when it comes to running E2E tests:

1. `test/Microsoft.Azure.Devices.Edge.Test/`: The actual tests live here, as do "fixtures" (setup and tear-down code that runs before/after/between tests) and the `Context` class (exposes the test arguments to tests).
2. `test/Microsoft.Azure.Devices.Edge.Test.Common/`: The helper library tests use to do interesting things--like deploy modules or wait for a message to arrive in IoT hub--lives here.
3. `test/modules`: All custom modules used by the tests live here. The common pattern for an end-to-end test is to deploy some modules that run through a scenario (i.e., the thing you actually want to test, like sending messages between modules), then wait for them to report the results.

## One time setup
To run the end-to-end tests, we will be building the required binaries from the code, build container images and push them to a local container registry. The tests will install the IoT Edge runtime from the binaries on your machine and run the containers using your local registry. 

To prepare, we will need to setup your local machine and cloud resources. The setup steps are detailed [here](./doc/one-time-setup.md)

## Build
#### Building the binaries
From the top folder of the codebase ( i.e., the iotedge folder), run the following(*Note: Some of these steps will require to be run as sudo*)
~~~ sh
# run build branch
scripts/linux/buildBranch.sh
# Build mqttd
# TODO: validate if the step to build mqttd can be skipped.
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

#### Building the container images and pushing them to the local container registry
From the top folder of the codebase, run the following. 
~~~ sh
# Consolidate artifacts for edge-hub
sudo scripts/linux/consolidate-build-artifacts.sh --artifact-name "edge-hub"

# Build and push images to local container registry
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
# steps to build and push this image to the local container registry.
sudo scripts/linux/cross-platform-rust-build.sh --os ubuntu18.04 --arch amd64 --build-path test/modules/generic-mqtt-tester/
cd test/modules/generic-mqtt-tester
sudo docker build --no-cache -t localhost:5000/microsoft/generic-mqtt-tester:latest-linux-amd64 --file docker/linux/amd64/Dockerfile --build-arg EXE_DIR=. target
sudo docker push localhost:5000/microsoft/generic-mqtt-tester:latest-linux-amd64
~~~

## Test

#### Test parameters

The end-to-end tests take several parameters, which they expect to find in a file named `context.json` in the same directory as the test binaries (e.g., `test/Microsoft.Azure.Devices.Edge.Test/bin/Debug/netcoreapp3.1/context.json`). Parameter names are case-insensitive. See [end-to-end test parameters](./doc/end-to-end-test-config.md) for details. 

#### Sample context.json
Here is a sample context.json file that you can use as a starting point to configure the parameters to fit your environment.
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
    "caCertScriptPath": "/home/azureuser/iotedge/tools/CACertificates/",
    "dpsIdScope": "0ne01F35169",
    "rootCaCertificatePath": "/home/azureuser/iotedge/tools/CACertificates/certs/certs/azure-iot-test-only.root.ca.cert.pem",
    "rootCaPrivateKeyPath": "/home/azureuser/iotedge/tools/CACertificates/certs/private/azure-iot-test-only.root.ca.key.pem",
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

#### Test secrets

The tests also expect to find several _secret_ parameters. It is recommended that you create environment variables and make them available to the test framework in a way that avoids committing them to your shell's command history or saving them in clear text on your filesystem.

See [environment variables](./doc/end-to-end-test-config.md#environment-variables) for details.

#### Run the tests

With the test parameters and secrets in place, you can run all the end-to-end tests from the command line. From the top folder of the codebase,

```bash
cd test
sudo --preserve-env dotnet test ./Microsoft.Azure.Devices.Edge.Test \
     --filter "TestCategory=EndToEnd&TestCategory!=NestedEdgeOnly"
```

To learn about other ways to run the tests (e.g., to run only certain tests), see 
[Running selective unit tests](https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests#nunit)

#### Troubleshooting

#### File handle limit in VS Code

If you are using VS Code in Linux, it is recommended that you increase the maximum number of file I/O handles to prevent tests from hitting the limit. To learn how to increase the file I/O capacity, see
https://code.visualstudio.com/docs/setup/linux#_visual-studio-code-is-unable-to-watch-for-file-changes-in-this-large-workspace-error-enospc.

_Note: You will need to increase both `fs.inotify.max_user_instances` and `fs.inotify.max_user_watches`_
