# End-to-end tests

## Test code structure

There are three directories under the `test/` directory:

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
./scripts/linux/buildBranch.sh

# setup environment variables
export PACKAGE_OS="ubuntu18.04"
export PACKAGE_ARCH="amd64"

# update sub modules [DOUBLE CHECK THIS]
git submodule update --init --recursive

# Create the installable packages. It will typically be located
# in the ./edgelet/target/release/ and the ./edgelet/target/hsm/ folders 
# and named something like aziot-edge*amd64.deb and libiothsm-std*.deb 

./edgelet/build/linux/package.sh

# copy the libiothsm*.deb file into the edgelet release directory, so that the
# tests can pickup both .deb files from the same folder.
cp edgelet/target/hsm/libiothsm*.deb edgelet/target/release

~~~

#### Building the container images and pushing them to the local container registry
From the top folder of the codebase, run the following. 
~~~ sh

# Build and push images to local container registry
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "edge-hub" -P "Microsoft.Azure.Devices.Edge.Hub.Service"  -v "latest" --bin-dir target
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
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "metrics-collector" -P "MetricsCollector"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "metrics-validator" -P "MetricsValidator"  -v "latest" --bin-dir target
sudo scripts/linux/buildImage.sh -r localhost:5000  -i "temperature-filter-function" -P "EdgeHubTriggerCSharp"  -v "latest" --bin-dir target

~~~

## Test

#### Test parameters

The end-to-end tests take several parameters, which they expect to find in a file named `context.json` in the same directory as the test binaries (e.g., `test/Microsoft.Azure.Devices.Edge.Test/bin/Debug/netcoreapp3.1/context.json`). Parameter names are case-insensitive. See [end-to-end test parameters](./doc/end-to-end-test-config.md) for details. 

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
    "diagnosticsImage": "localhost:5000/microsoft/diagnostics:latest-linux-amd64",
    "metricsCollectorImage": "localhost:5000/microsoft/metrics-collector:latest-linux-amd64",
    "metricsValidatorImage": "localhost:5000/microsoft/metrics-validator:latest-linux-amd64",
    "caCertScriptPath": "/home/azureuser/iotedge/test/certs",
    "dpsIdScope": "0ne01F35169",
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
