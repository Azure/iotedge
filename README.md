# Azure IoT Edge

This repository consists of two projects: the Module Management Agent (edge-agent) and the Edge Hub (edge-hub).

## Build

Make sure the following dependencies are installed in your environment before you build IoT Edge code:

| Dependency        | Notes                |
|-------------------|----------------------|
| .NET Core 2.0     | Installation instructions [here](https://www.microsoft.com/net/core/preview). |
| Java              | Not needed if building in VS IDE (Windows). Otherwise, JRE is required to compile the Antlr4 grammar files into C# classes, and `java` must be on your path. |

Besides using Visual Studio IDE in Windows, you can build by running the build script:

### Linux
```
scripts/linux/buildBranch.sh
```

### Windows
```
scripts\windows\buildBranch.bat
```

Binaries are published to `target/publish/`.

## Run unit tests

Besides using Test Explorer in Visual Studio IDE, you can run the unit tests with:

### Linux
```
scripts/linux/runTests.sh
```

### Windows
```
scripts\windows\runTests.bat
```

## Run integration tests & BVTs

To run integration tests and/or BVTs, make sure the following dependencies are installed in your environment:

| Dependency        | Notes                |
|-------------------|----------------------|
| Azure CLI         | Installation instructions [here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) |
| Powershell        | Installation instructions [here](https://github.com/PowerShell/PowerShell/tree/master/docs/installation) |
| Jq                | Installation instructions [here](https://stedolan.github.io/jq/download/) |
| Docker            | Installation instructions [here](https://docs.docker.com/engine/installation/#supported-platforms). In Linux environments, be sure to follow the [post-installation steps](https://docs.docker.com/engine/installation/linux/linux-postinstall/) so the tests can run without `sudo`. |

The integration tests and BVTs expect to find certain values in an Azure KeyVault (see `edge-util/test/Microsoft.Azure.Devices.Edge.Util.Test.Common/settings/base.json`). For the tests to access the KeyVault at runtime, a certificate must first be installed in the environment where the tests will run. Install the KeyVault certificate with:

### Linux
```
scripts/linux/downloadAndInstallCert.sh <SpUsername> <SpPassword> <AadTenant> <CertName> <VaultName>
```

| Argument    | Description                |
|-------------|----------------------------|
| SpUsername  | Service principal username. See `az login` [help](https://docs.microsoft.com/en-us/cli/azure/#login). |
| SpPassword  | Service principal password. See `az login` [help](https://docs.microsoft.com/en-us/cli/azure/#login). |
| AadTenant   | Azure Active Directory tenant. See `az login` [help](https://docs.microsoft.com/en-us/cli/azure/#login). |
| CertName    | Certificate name. See `--secret` in `az keyvault secret show` [help](https://docs.microsoft.com/en-us/cli/azure/keyvault/secret#show). |
| VaultName   | KeyVault name. See `az keyvault secret show` [help](https://docs.microsoft.com/en-us/cli/azure/keyvault/secret#show). |

### Windows
```
powershell scripts\windows\DownloadAndInstallCertificate.ps1 <VaultName> <CertificateName>
```

| Argument    | Description                |
|-------------|----------------------------|
| VaultName   | KeyVault name. See `Get-​Azure​Key​Vault​Secret` [help](https://docs.microsoft.com/en-us/powershell/module/azurerm.keyvault/get-azurekeyvaultsecret). |
| CertName    | Certificate name. See `Get-​Azure​Key​Vault​Secret` [help](https://docs.microsoft.com/en-us/powershell/module/azurerm.keyvault/get-azurekeyvaultsecret). |

Then run the tests either with Test Explorer in Visual Studio IDE, or with:

### Linux
```
scripts/linux/runTests.sh "--filter Category=Integration|Category=Bvt"
```

### Windows
```
scripts\windows\runTests.bat "--filter Category=Integration|Category=Bvt"
```

The syntax of the "filter" argument is described [here](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test#filter-option-details). All IoT Edge tests are categorized as one of `Unit`, `Integration`, or `Bvt`.

## Build Manifest Image
There is a script in the repo to build multi-architecture images.
This scripts assumes that the platform specific images are already in the docker registry.
Usage is as follows:
```
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
