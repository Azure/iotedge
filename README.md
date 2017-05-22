# Azure IoT Edge

This repository consists of two projects: the Module Management Agent (edge-agent) and the Edge Hub (edge-hub).

## How to Build

### Linux (Ubuntu 14.04 Trusty)

#### Dependencies

##### .NET Core 2.0
The Azure IoT Edge projects uses .NET Core 2.0. This is currently in preview, but can still be installed via apt-get.
Please follow the [official instructions](https://www.microsoft.com/net/core/preview#linuxubuntu). However, the basic gist is as
follows:

```
sudo sh -c 'echo "deb [arch=amd64] https://apt-mo.trafficmanager.net/repos/dotnet-release/ trusty main" > /etc/apt/sources.list.d/dotnetdev.list'
sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 417A0893
sudo apt-get update

sudo apt-get install dotnet-dev-2.0.0-preview1-005977
```

##### Java
Java (JRE) is required to compile the Antlr4 grammar files into C# classes. A JRE is required and `java` must be
on your path.

#### Build
You can build by running the build script:

```
./scripts/linux/buildBranch.sh
```

This will publish to `./target/publish`.

You can run the tests with:

```
./scripts/linux/runTests.sh
```
