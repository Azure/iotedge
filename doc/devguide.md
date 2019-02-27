# Dev Guide

IoT Edge is written in C# and Rust.
The C# development setup is described below. The Rust development setup is described [here](../edgelet/README.md).

## Setup

Make sure the following dependencies are installed in your environment before you build IoT Edge code:

| Dependency        | Notes                |
|-------------------|----------------------|
| .NET Core 2.1     | Installation instructions [here](https://www.microsoft.com/net/core). |
| Java              | Not needed if building in VS IDE (Windows). Otherwise, a JRE is required to compile the Antlr4 grammar files into C# classes, and `java` must be on your path. |

## Build

Besides using Visual Studio in Windows, you can build by running the build script:

### Linux
```sh
scripts/linux/buildBranch.sh
```

### Windows
```powershell
scripts\windows\buildBranch.bat
```

Binaries are published to `target/publish/`.

## Run unit tests

Besides using Test Explorer in Visual Studio, you can run the unit tests with:

### Linux
```sh
scripts/linux/runTests.sh
```

### Windows
```powershell
scripts\windows\runTests.bat
```

## Run integration tests

To run integration tests and/or BVTs, make sure the following dependencies are installed in your environment:

| Dependency        | Notes                |
|-------------------|----------------------|
| Azure CLI         | Installation instructions [here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) |
| Powershell        | Installation instructions [here](https://docs.microsoft.com/en-us/powershell/scripting/setup/installing-powershell-core-on-linux) |
| Jq                | Installation instructions [here](https://stedolan.github.io/jq/download/) |
| Docker            | Installation instructions [here](https://docs.docker.com/engine/installation/#supported-platforms). In Linux environments, be sure to follow the [post-installation steps](https://docs.docker.com/engine/installation/linux/linux-postinstall/) so the tests can run without `sudo`. |

The integration tests and BVTs expect to find certain values in an Azure KeyVault (see `edge-util/test/Microsoft.Azure.Devices.Edge.Util.Test.Common/settings/base.json`). For the tests to access the KeyVault at runtime, a certificate must first be installed in the environment where the tests will run. Install the KeyVault certificate with:

### Linux
```sh
az login # Login and select default subscription, if necessary

scripts/linux/downloadAndInstallCert.sh -v <VaultName> -c <CertName>
```

| Argument    | Description                |
|-------------|----------------------------|
| VaultName   | KeyVault name. See `az keyvault secret show` [help](https://docs.microsoft.com/en-us/cli/azure/keyvault/secret#show). |
| CertName    | Certificate name. See `--secret` in `az keyvault secret show` [help](https://docs.microsoft.com/en-us/cli/azure/keyvault/secret#show). |

### Windows
```powershell
Connect-AzureRmAccount # Login and select default subscription, if necessary

scripts\windows\setup\Install-VaultCertificate.ps1 -VaultName <VaultName> -CertificateName <CertificateName>
```

| Argument    | Description                |
|-------------|----------------------------|
| VaultName   | KeyVault name. See `Get-AzureKeyVaultSecret` [help](https://docs.microsoft.com/en-us/powershell/module/azurerm.keyvault/get-azurekeyvaultsecret). |
| CertName    | Certificate name. See `Get-AzureKeyVaultSecret` [help](https://docs.microsoft.com/en-us/powershell/module/azurerm.keyvault/get-azurekeyvaultsecret). |

Then run the tests either with Test Explorer in Visual Studio IDE, or with:

### Linux
```sh
scripts/linux/runTests.sh "--filter Category=Integration"
```

### Windows
```powershell
scripts\windows\runTests.bat "--filter Category=Integration"
```

The syntax of the "filter" argument is described [here](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test#filter-option-details). All IoT Edge tests are categorized as one of `Unit`, `Integration`, or `Bvt`.

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
