# Dev Guide

IoT Edge is written in C# and Rust.
The C# development setup is described below. The Rust development setup is described [here](../edgelet/README.md).

If you want to run tests outside of the pipelines, you will need to be running linux.

## Setup

Make sure the following dependencies are installed in your environment before you build IoT Edge code:

| Dependency    | Notes                                                                                                                                                          |
| ------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| .NET 8.0 | Installation instructions [here](https://www.microsoft.com/net/core).                                                                                          |
| Java          | Not needed if building in VS IDE (Windows). Otherwise, a JRE is required to compile the Antlr4 grammar files into C# classes, and `java` must be on your path. |

## Build

Besides using Visual Studio in Windows, you can build by running the build script:

```sh
scripts/linux/buildBranch.sh
```

Binaries are published to `target/publish/`.

### Select an existing Buildx builder for Linux container builds

`scripts/linux/buildImage.sh` and `scripts/linux/buildRocksDb.sh` accept `--builder NAME` or the standard `BUILDX_BUILDER` environment variable; the command-line option takes precedence. With no command-line selection and `BUILDX_BUILDER` unset or empty, the scripts retain the existing workflow: create a temporary `docker-container` builder and remove it on exit. An explicit selection reuses an externally provisioned, existing builder without creating or removing it. In particular, `default` selects Docker Engine's embedded BuildKit (`docker` driver), not a new builder container.

After building the application binaries and authenticating to your registry, for example:

```sh
scripts/linux/buildImage.sh \
  --app Microsoft.Azure.Devices.Edge.Hub.Service --bin target \
  --name azureiotedge-hub --registry myregistry.azurecr.io \
  --version dev --builder default

BUILDX_BUILDER=default scripts/linux/buildRocksDb.sh \
  --output-dir target --build-number dev --arch amd64
```

The shared Azure Pipelines templates expose the same optional `builder` parameter:

```yaml
- template: /builds/misc/templates/build-images.yaml
  parameters:
    builder: default
```

This selects the builder for RocksDB and every Linux image job. The leaf templates (`build-rocksdb.yaml` and `image-linux.yaml`) also accept it directly. The default is `''`, which leaves an inherited `BUILDX_BUILDER` unchanged; without an environment selection, the temporary-builder workflow is unchanged.

Before opting in on each build agent:

- The embedded `docker` driver needs the **containerd image store** for multi-platform images and provenance attestations. Fresh Engine installations may enable it by default, while upgraded installations can retain the classic image store; do not infer support from the Engine version alone. Check `docker info` for `driver-type: io.containerd.snapshotter.v1`, and run `docker buildx inspect default --bootstrap` to check the driver, embedded BuildKit version and advertised platforms. For another existing builder, inspect its name instead.
- The selected BuildKit and Buildx versions must support SLSA v1 provenance with `--provenance=mode=min,version=v1`. Image builds explicitly request that format and `--pull` to refresh base images. The release refresh detector consumes the SLSA v1 dependency metadata; older BuildKit versions that cannot emit it are not supported.
- Cross-architecture Dockerfile `RUN` steps still require native workers or working emulation (for example, binfmt/QEMU). RocksDB retains `--load`, and its subsequent `docker run --platform ...` must execute the target architecture on the local Docker daemon too; a remote builder alone does not provide that runtime support.

Provision compatible builders, daemon settings and emulation outside these scripts. They do not automatically modify daemon configuration or fall back to another builder when the selected one is incompatible.

OneBranch release definitions live in the separate `iotedge-release` repository. They must explicitly opt in and exercise image publishing and the refresh detector (`builds/release/detect-image-updates.yaml`) end to end before rollout; these shared-template changes alone do not enable release builds. Other Docker Hub references, including API proxy base images, are not addressed by builder selection or this change.

## Run unit tests

Besides using Test Explorer in Visual Studio, you can run the unit tests with:

```sh
scripts/linux/runTests.sh
```

The Linux container-build script regression tests require Python 3 and Bash, but no Docker daemon or registry access:

```sh
python3 -B -m unittest discover -s scripts/linux/tests -p test_buildx_scripts.py -v
```

## Run integration tests

To run integration tests and/or BVTs, make sure the following dependencies are installed in your environment:

| Dependency | Notes                                                                                                                                                                                                                                                                                 |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Azure CLI  | Installation instructions [here](https://docs.microsoft.com/cli/azure/install-azure-cli).                                                                                                                                                                                             |
| Powershell | Installation instructions [here](https://docs.microsoft.com/powershell/scripting/install/installing-powershell).                                                                                                                                                                      |
| Jq         | Installation instructions [here](https://stedolan.github.io/jq/download/).                                                                                                                                                                                                            |
| Docker     | Installation instructions [here](https://docs.docker.com/engine/installation/#supported-platforms). In Linux environments, be sure to follow the [post-installation steps](https://docs.docker.com/engine/installation/linux/linux-postinstall/) so the tests can run without `sudo`. |

The integration tests and BVTs expect to find certain values in an Azure KeyVault (see `edge-util/test/Microsoft.Azure.Devices.Edge.Util.Test.Common/settings/base.json`). For the tests to access the KeyVault at runtime, a certificate must first be installed in the environment where the tests will run. Install the KeyVault certificate with:

```sh
az login # Login and select default subscription, if necessary

scripts/linux/downloadAndInstallCert.sh -v <VaultName> -c <CertName>
```

| Argument  | Description                                                                                                                                         |
| --------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| VaultName | KeyVault name. See `az keyvault secret show` [help](https://docs.microsoft.com/cli/azure/keyvault/secret#az_keyvault_secret_show).                  |
| CertName  | Certificate name. See `--secret` in `az keyvault secret show` [help](https://docs.microsoft.com/cli/azure/keyvault/secret#az_keyvault_secret_show). |

Then run the tests either with Test Explorer in Visual Studio IDE, or with:

```sh
scripts/linux/runTests.sh "Category=Integration"
```

The syntax of the "filter" argument is described [here](https://docs.microsoft.com/dotnet/core/tools/dotnet-test#filter-option-details). All IoT Edge tests are categorized as one of `Unit`, `Integration`, or `Bvt`.

## Run the end-to-end tests

The end-to-end tests are documented [here](../test/README.md).


## Running Code Coverage Checks for Unit Tests Locally (Windows Only)

Currently, Code Coverage Local Checks are supported only in a Windows Environment due to dependency on Microsoft Code Coverage Tools

1. Use Command Prompt to run dotnet test from root directory

```pwsh
dotnet test /p:CollectCoverage=true --filter "Category=Unit" -s CodeCoverage.runsettings --logger:trx --results-directory TestResults
```

2. Convert *.coverage to *.coveragexml file to generate HTML Reports

```pwsh
dotnet tool install --global dotnet-coverageconverter
dotnet-coverageconverter --CoverageFilesFolder TestResults
```

3. Generate HTML Report from Coverage Files. The HTML Coverage Files will be generated in *reports* folder in the root directory

```pwsh
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator "-reports:TestResults\*\*.coveragexml" "-targetdir:report"
```



## Build Edge Hub Container Locally

Sometimes it is useful to build the Edge Hub container locally. If you want to do so you can run the below script:
```
./scripts/linux/buildLocalEdgeHub.sh --registry-address "$(registry.address)" --version "$(version)"
```

## Attach the VSCode Debugger to EdgeAgent
There is a script in the repo to setup a docker container with the Visual Studio Debugger (vsdbg).  After running the script in a container, you can connect the VSCode debugger to a process running in the container. The following example shows how to run the setup script on a Linux IoT Edge device to setup the debugger in the Edge Agent container:

```
$ scripts/linux/setupContainerDebugger.sh -c edgeAgent -u edgeagentuser
```
After running the debugger setup script, create a launch.json file in the edgeAgent/.vscode directory. The launch.json file should have the following contents (Note: replace the value in `sourceFileMap` before running):
```
{
    // Use IntelliSense to learn about possible attributes.
    // Hover to view descriptions of existing attributes.
    // For more information, visit: https://go.microsoft.com/fwlink/?linkid=830387
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Remote Debug IoT Edge Module (.NET Core)",
            "type": "coreclr",
            "request": "attach",
            "processId": "${command:pickRemoteProcess}",
            "pipeTransport": {
                "pipeProgram": "docker",
                "pipeArgs": [
                    "exec",
                    "-i",
                    "-u",
                    "edgeagentuser"
                    "edgeAgent",
                    "sh",
                    "-c"
                ],
                "debuggerPath": "/root/vsdbg/vsdbg",
                "pipeCwd": "${workspaceFolder}",
                "quoteArgs": true
            },
            "sourceFileMap": {
                "<replace-with-compile-time-path-to-edge-agent-source>": "${workspaceRoot}"
            },
            "symbolOptions": {
                "searchPaths": ["/app"],
                "searchMicrosoftSymbolServer": false,
                "searchNuGetOrgSymbolServer": false
            },
            "justMyCode": true,
            "requireExactSource": true
        }
    ]
}
```
Start debugging by selecting the configuration defined above from the 'Run and Debug' tab (Ctrl+Shift+D) and selecting the 'Start Debugging' button (F5). 