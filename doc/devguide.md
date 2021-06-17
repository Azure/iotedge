# Dev Guide

IoT Edge is written in C# and Rust.
The C# development setup is described below. The Rust development setup is described [here](../edgelet/README.md).

If you want to run tests outside of the pipelines, you will need to be running linux.

## Setup

Make sure the following dependencies are installed in your environment before you build IoT Edge code:

| Dependency        | Notes                |
|-------------------|----------------------|
| .NET Core 3.1     | Installation instructions [here](https://www.microsoft.com/net/core). |
| Java              | Not needed if building in VS IDE (Windows). Otherwise, a JRE is required to compile the Antlr4 grammar files into C# classes, and `java` must be on your path. |

## Build

Besides using Visual Studio in Windows, you can build by running the build script:

```sh
scripts/linux/buildBranch.sh
```

Binaries are published to `target/publish/`.

## Run unit tests

Besides using Test Explorer in Visual Studio, you can run the unit tests with:

```sh
scripts/linux/runTests.sh
```

## Run integration tests

To run integration tests and/or BVTs, make sure the following dependencies are installed in your environment:

| Dependency        | Notes                |
|-------------------|----------------------|
| Azure CLI         | Installation instructions [here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli). |
| Powershell        | Installation instructions [here](https://docs.microsoft.com/en-us/powershell/scripting/setup/installing-powershell-core-on-linux). |
| Jq                | Installation instructions [here](https://stedolan.github.io/jq/download/). |
| Docker            | Installation instructions [here](https://docs.docker.com/engine/installation/#supported-platforms). In Linux environments, be sure to follow the [post-installation steps](https://docs.docker.com/engine/installation/linux/linux-postinstall/) so the tests can run without `sudo`. |

The integration tests and BVTs expect to find certain values in an Azure KeyVault (see `edge-util/test/Microsoft.Azure.Devices.Edge.Util.Test.Common/settings/base.json`). For the tests to access the KeyVault at runtime, a certificate must first be installed in the environment where the tests will run. Install the KeyVault certificate with:

```sh
az login # Login and select default subscription, if necessary

scripts/linux/downloadAndInstallCert.sh -v <VaultName> -c <CertName>
```

| Argument    | Description                |
|-------------|----------------------------|
| VaultName   | KeyVault name. See `az keyvault secret show` [help](https://docs.microsoft.com/en-us/cli/azure/keyvault/secret#show). |
| CertName    | Certificate name. See `--secret` in `az keyvault secret show` [help](https://docs.microsoft.com/en-us/cli/azure/keyvault/secret#show). |

Then run the tests either with Test Explorer in Visual Studio IDE, or with:

```sh
scripts/linux/runTests.sh "--filter Category=Integration"
```

The syntax of the "filter" argument is described [here](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test#filter-option-details). All IoT Edge tests are categorized as one of `Unit`, `Integration`, or `Bvt`.

## Run the end-to-end tests

The end-to-end tests are documented [here](../test/README.md).

## Build Edge Hub Container Locally

Sometimes it is useful to build the Edge Hub container locally. If you want to do so you can run the below set of scripts:
```
scripts/linux/buildBranch.sh
scripts/linux/cross-platform-rust-build.sh --os alpine --arch amd64 --build-path mqtt/mqttd
scripts/linux/cross-platform-rust-build.sh --os alpine --arch amd64 --build-path edge-hub/watchdog
scripts/linux/consolidate-build-artifacts.sh --artifact-name "edge-hub"
scripts/linux/buildImage.sh -r "$(registry.address)" -u "$(registry.user)" -p "$(registry.password)" -i "${{ parameters.imageName }}" -n "${{ parameters.namespace }}" -P "${{ parameters.project }}" -v "${{ parameters.version }} --bin-dir target"
```

## Build Manifest Image
There is a script in the repo to build multi-architecture images.
This script assumes that the platform specific images are already in the docker registry.
Usage is as follows:
```sh
$ scripts/linux/buildManifest.sh --help

buildManifest.sh [options]
Note: Depending on the options you might have to run this as root or sudo.

options
 -r, --registry       Docker registry required to build, tag and run the module
 -u, --username       Docker Registry Username
 -p, --password       Docker Username's password
 -v, --image-version  Docker Image Version.
 -t, --template       Yaml file template for manifest definition.
```
