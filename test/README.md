# End-to-end tests

## Test code structure

There are three directories under the `test/` directory:

1. `test/Microsoft.Azure.Devices.Edge.Test/`: The actual tests live here, as do "fixtures" (setup and tear-down code that runs before/after/between tests) and the `Context` class (exposes the test arguments to tests).
2. `test/Microsoft.Azure.Devices.Edge.Test.Common/`: The helper library tests use to do interesting things--like deploy modules or wait for a message to arrive in IoT hub--lives here.
3. `test/modules`: All custom modules used by the tests live here. The common pattern for an end-to-end test is to deploy some modules that run through a scenario (i.e., the thing you actually want to test, like sending messages between modules), then wait for them to report the results.

## One time setup
*Note: The steps mentioned here have been run and validated on an ubuntu18.04/amd64 machine. If you are running a different os/arch, your steps may differ.*

## One time setup for your local machine
To run the end-to-end tests, we will be building the required binaries from the code, build container images and push them to a local container registry. The tests will install the IoT Edge runtime from the binaries on your machine and run the containers using your local registry.

##### Prerequisites

It is important that your machine meets the requirements to run IoT Edge. See our installation docs ([Linux](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux), [Windows](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-windows)) for more information on prerequisites

##### Install java
The build scripts use Java for code generation tasks, so you will need a jdk on your machine. Here is how you can install a jdk (if you don't already have one).
~~~ sh
ubuntu_release=`lsb_release -rs`
wget https://packages.microsoft.com/config/ubuntu/${ubuntu_release}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb

sudo apt-get install apt-transport-https
sudo apt-get update
sudo apt-get install msopenjdk-11
~~~

The steps above are for ubuntu 18.04. See [Install the Microsoft Build of the OpenJDK](https://docs.microsoft.com/en-us/java/openjdk/install) for information on how to install on other platforms.

##### Setup access to Microsoft installation packages
Prepare your machine to access the Microsoft installation packages. The steps to do this are listed below for your convenience. *Again, please look at the section in the linked document for your os/arch combination if it is different from ubuntu18.04/amd64*

_TODO: Access to Microsoft installation packages is required as this codebase depends on aziot-identity-service. Resolve this dependency by building from the identity service's code repository_

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

##### Create Root CA Certificate for your machine
Create a Root CA certificate for your machine *(See [Create Certs](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-create-test-certificates?view=iotedge-2020-11#create-root-ca-certificate) for more details)*. Navigate to the directory where the certificate generation scripts are found (typically iotedge/tools/CACertificates) and run
~~~ sh
./certGen.sh create_root_and_intermediate
# This will create root and intermediate CA certs. The Root CA cert will be located in the 
# certs sub directory and will be needed to run the tests.
~~~

##### Configure your local container registry
We will be building the container images from the codebase and pushing them to a local container registry. To prepare, we will install the moby engine and configure the container registry using the steps below. 

###### Install moby engine
See [Install a container engine](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge?view=iotedge-2020-11#install-a-container-engine) for details

~~~ sh
sudo apt-get install moby-engine
~~~
###### Create TLS certificates
We will be running the container registry with TLS and authentication turned on. If you already have certs for TLS, feel free to use those and skip the cert creation steps listed below. 
In a convenient directory, create a folder called `auth` to store your keys and certs (*I have used /home/azureuser/auth*)
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
Next, we will setup basic username/password authentication for the registry using [htpasswd](http://httpd.apache.org/docs/current/programs/htpasswd.html).
~~~ sh
sudo apt-get install apache2-utils
# Create password file
: | sudo tee htpasswd
# Create username and password. Replace the username and password strings 
# below with a user/password combo that you like
echo "password" | sudo htpasswd -iB htpasswd username
~~~

###### Configure and run the local container registry
~~~ sh
# Set up the container registry to listen on port 5000 and to restart automatically.
# Mount the auth folder into the container and point to the TLS and Basic auth files
# Restart is set to always - This will ensure that the registry is always up.
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
# If all goes well, you will see the something like below when you run docker ps.
# 59867fe47a5b   registry:2   "/entrypoint.sh /etc…"   6 seconds ago   Up 6 seconds   0.0.0.0:5000->5000/tcp, :::5000->5000/tcp   registry
sudo docker ps

# Run docker login to validate your setup and verify that your login/password works. 
# The login step will cache your credentials on your machine and will be required later 
# when you build and push docker images

sudo docker login localhost:5000
~~~

##### Install .NET
The end-to-end tests are written in .NET Core and run with the `dotnet test` command, and you need to install .NET Core SDK. See [Install the .NET SDK](https://docs.microsoft.com/en-us/dotnet/core/install/sdk) for further information. *Note: You will have to install .NET version 3.1.*

Here is a convenient way to install the SDK.
~~~ sh
curl -sL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod u+x dotnet-install.sh
./dotnet-install.sh -c Current
~~~

## One time cloud resources setup for the tests
The end-to-end tests require a number of azure cloud side resources i.e., IoTHub, Device Provisioning Service, and a Storage Container to be setup. This next section will walk you through how to setup the cloud resources.

##### Create a resource group
If you don't already have a resource group, create one.
Here is how you can create one using the CLI
~~~ sh
az group create --name {resource group name} --location {region}
~~~

##### IoT Hub
If you don't already have an existing IoT hub, create one. There is no special configuration required, except for making sure that your IoTHub is enabled for public access.
Here is how you can create one using the CLI

~~~sh
# Create a free tier Iot hub
az iot hub create --resource-group {resource group name} --name {IoT hub name} --sku F1 --partition-count 2
~~~

Note down the `event hub compatible endpoint` and the primary connection string of the `iothubowner` policy. These will need to be set in the `E2E_EVENT_HUB_ENDPOINT` and `E2E_IOT_HUB_CONNECTION_STRING` environment variables later to run the tests.

You can get these via the CLI by running 
~~~sh
# Primary Connection string
az iot hub connection-string show --hub-name {IoT hub name} --key-type primary

#Default eventhub compatible end point
az iot hub connection-string show --hub-name {IoT hub name} --default-eventhub

~~~
##### Device Provisioning Service (DPS)
Create a DPS instance. A subset of the end-to-end tests will use DPS for testing device provisioning scenarios. Note down the `ID Scope` of this DPS instance. You will need to set it in the `dpsIdScope` configuration variable later to run the tests.

Using the CLI,
~~~sh
az iot dps create --name {dps group name} --resource-group {resource group name} --location {region}
~~~

###### Create enrollment group for symmetric key based enrollment
In your DPS instance, create a new enrollment group for symmetric key based enrollment. For this enrollment group, set the attestation type to be symmetric key, set the IoTEdge Device setting to true, and link the group to your IoThub with the access policy of `iotHubOwner`. 

Using the CLI,
~~~sh
#Link the DPS group to your Iot hub
az iot dps linked-hub create --dps-name {dps group name} --resource-group {resource group name} \
  --connection-string "{Iothub ConnectionString}" --location westus2

#Create enrollment group for symmetric key based enrollment
 az iot dps enrollment-group create -g {resource group name} --dps-name {dps group name} \
  --enrollment-id {symkey enrollment group name } --edge-enabled
~~~
You do not have to create the symmetric key. The system will auto generate it for you when the enrollment group is created. After the enrollment group is created, note down the Primary Key. You will need to set this in the `E2E_DPS_GROUP_KEY` environment variable later to run the tests.

See [Symmetric key attestation](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-auto-provision-symmetric-keys?view=iotedge-2020-11&tabs=windows) for further details.

###### Create enrollment group for X.509 certificate based enrollment
In your DPS instance, create another enrollment group for X.509 certificate based enrollment. For this enrollment group, set the attestation type to be certificate and upload the root CA certificate that you had created earlier, set the IoTEdge Device setting to true, and link the group to your IotHub with the access policy of `iotHubOwner`.

~~~sh
# Upload the root ca cert and set it to be verified
az iot dps certificate create --certificate-name {dps root ca name}  --resource-group {resource group name} \
  --dps-name {dps group name} --path {path to ca cert .pem file}  --verified true

# Create enrollment group for X.509 based enrollment
az iot dps enrollment-group create -g {resource group name} --dps-name {dps group name} \
  --enrollment-id {cert enrollment group name} --ca-name {dps root ca name} --edge-enabled
~~~
See [X.509 certificate attestation](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-auto-provision-x509-certs?view=iotedge-2020-11&tabs=windows) for further details.

##### Create a storage account/container
Create a storage account and storage container, with access level set to private. A subset of the tests will use the storage container to test log/diagnostics upload scenarios. To securely acesss the storage container, create a SAS token URL ( allow https only, permissions set to "write" only). Note down this URL as you will need it to set the `E2E_BLOB_STORE_SAS` environment variable later to run the tests.
~~~ sh
# Create storage account
az storage account create --name {storage account name} --resource-group {resource group name } \
  --location {region} --sku Standard_RAGRS --kind StorageV2

# Get the connection string from the Storage account. This will be needed to create the storage container
az storage account show-connection-string --name {storage account name} \
  --resource-group {resource group name} -o tsv

# Create the storage container
 az storage container create --name {container name} --resource-group {resource group name} \
   --account-name {storage account name} --connection-string "{connection string}"

# TODO: Currently, the SAS URL has to created using the Azure portal. Add instructions
# here to do it using the CLI
~~~

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
