# Azure IoT Edge Runtime Control

## Introduction
The azure-iot-edge-ctl script assists a user in managing and controlling the Edge runtime.

Specifically, it can help in:
* Initial Bootstrap
* Certificate provisioning
* Starting/Stopping and other Edge runtime options.


## Components

For the purposes of this README, the directory containing these scripts is called the EDGESCRIPTSDIR. Here are the contents of this directory:
```
EDGESCRIPTSDIR
    .
    +-- azure-iot-edge-ctl - Main script to control the Edge runtime
    +-- config
    |   +-- Reference Edge configuration file(s).
    +-- deployment-docker
    |   +-- launch-edge - Internal script(s) to start/stop/control the Edge runtime
    +-- gen-certs
    |   +-- generate-certs - Internal script(s) to assist the user in generating self signed certificates.
    |   +-- openssl configuration files
    |   +-- README.md
    +-- README.md
```

## Prerequisites:
    1. Linux/MacOS
      * Bash
      * OpenSSL
      * JQ
    2. Windows
      * OpenSSL
      * TBD

## How To Run:
    Linux/MacOS: ./azure-iot-edge-ctl --help
        Windows: TBD


## Edge Host Filesystem Description

The following section describes the files required on the Edge host/gateway device required to bootstrap the Azure IoT Edge runtime. Setting up the files needed for the Edge runtime is very straight forward, overall it involves two steps:

1. Creating a directory which will be used by the Edge runtime. This directory is termed as *EDGEHOMEDIR*.
2. Copying and modifying a configuration file called the Edge Host Configuration File. This file is a JSON formatted configuration file which contains configuration values required to run the Edge runtime.
    - Note: A configuration file with default settings can be found here ```EDGESCRIPTSDIR/config/azure-iot-edge-config-reference.json```

Feel free to skip this section and jump straight to [Setup](#Setup).

### Edge Home Directory Description
The Edge runtime needs a directory on the host machine in order to execute. This directory will contain the necessary configuration, certificates and module specific files.

Users have the option of setting up this directory anywhere they like OR use an installer (if available) to setup the Edge at a default directory. The default directories are listed below for the various host OSes:

```
Default Host Paths:
-------------------
    Linux:   /usr/local/azure-iot-edge
    Windows: TBD
    MacOS:   TBD
```

As the Edge runtime is executed, the following file system structure is created under *EDGEHOMEDIR*.

```
EDGEHOMEDIR Structure:
-----------------------
    EDGEHOMEDIR
        .
        +-- config  -- Edge configuration file(s) read by the azure-iot-edge-ctl scripts to setup,
        |              deploy and execute the Edge runtime.
        +-- certs   -- This directory is created by the scripts when generating self signed certificates.
        |
        +-- modules -- This is directory that will be created by the azure-iot-edge-ctl scripts
                       and it used to host all the Edge Module specific files.
```

### Edge Host Configuration File Description

The following section goes into details of the various configuration items and lays out how users are expected to modify this.

```
  // Config file format schema; Users should not need to modify this.
  "schemaVersion": "1",

  // User's IoTHub Device Connection string in the format listed below.
  "deviceConnectionString": "HostName=<>;DeviceId=<>;SharedAccessKey=<>",

  // Path to the Edge home dir,
  "homeDir": "<EDGEHOMEDIR>",

  // Edge device's fully qualified DNS name; This is needed for certificate
  // generation for the Edge Hub server. This certificate is used to enable
  // TLS connections from Edge modules and leaf devices.
  "hostName": "<Hostname>",

  // Log level setting for Edge runtime diagnostics. "info" and "debug".
  // are the supported levels and default is info. User should only
  // modify this for debugging purposes.
  "logLevel": "info",

  // Configuration settings for the Edge Runtime
  "security": {

    // Configuration of X.509 certificates; There are two options:
    //  - Self Signed Certificates:   This mode is NOT secure and is only
    //    (selfSigned)                intended for development purposes
    //                                and quick start type scenarios.
    //
    //  - Pre Installed Certificates: When this is enabled, users are
    //    (preInstalled)              expected to supply a "Device CA"
    //                                certificate and a Edgehub server
    //                                certificate signed by this
    //                                Device CA cert. This is more of
    //                                a real world setup.
    // The "option" key below selects any of the modes listed above.
    "certificates": {
      "option": "selfSigned",
      "selfSigned": {
        "forceRegenerate": false
      },

      "preInstalled": {
        "deviceCACertificateFilePath": "",
        "serverCertificateFilePath": ""
      }
    }
  },
  // Section containing Configuration of Edge Runtime Deployment and Host.
  "runtime": {

    // Currently "docker" is the only deployment type supported.
    "type": "docker",

    // Docker host settings
    "docker": {
      // Docker Daemon Socket URI; Under normal circumstances. Users should
      // modify this according to what is supported on their host
      "uri": "unix:///var/run/docker.sock",

      // Edge runtime image; Users may have to update this as newer images
      // are released over time.
      "edgeRuntimeImage": "edge_repository_address/edge_image_name:version",

      // Users can add registries in this array for their custom modules.
      // If there is no username or password associated with a repository,
      // users should set the values as "".
      // NOTE: This is a temporary configuration item required by the Edge
      // Longer term, users would be able to manage their repositories and
      // credentials in the cloud using the IoTHub portal.
      "registries": [
        {
          "address": "example-repository-address-1",
          "username": "example-username-1",
          "password": "example-password-1"
        },
        {
          "address": "example-repository-address-2",
          "username": "example-username-2",
          "password": "example-password-2"
        }
      ],

      // Logging options for the Edge runtime. The format complies with
      // the docker schema described here:
      // https://docs.docker.com/engine/admin/logging/overview/
      "loggingOptions": {
        "log-driver": "json-file",
        "log-opts": {
          "max-size": "10m"
        }
      }
    }
  }
```

## Setup

Users have the following choices to setup their environment:

### 1) Using an Installer
The installer performs the following steps:
* Creates the default EDGEHOMEDIR directory on a host and sets up environment variable EDGEHOMEDIR
  * ```Linux/MacOS: export EDGEHOMEDIR=<path>```
  * ```    Windows: set EDGEHOMEDIR=<path>```
* Next it copies the reference configuration file
 * FROM:  ```EDGESCRIPTSDIR/config/azure-iot-edge-config-reference.json```
 * TO:    ```EDGEHOMEDIR/config/azure-iot-edge-config.json```
* Prompts the user to fill up any required configuration items.

### 2) Manual Onetime Setup
For platforms where an installer is not available, a manual setup can be performed as follows:
* Create a directory on a host and setup environment variable EDGEHOMEDIR
  * ```Linux/MacOS: export EDGEHOMEDIR=<path>```
  * ```    Windows: set EDGEHOMEDIR=<path>```
* Copy the reference configuration file
 * FROM:  ```EDGESCRIPTSDIR/config/azure-iot-edge-config-reference.json```
 * TO:    ```EDGEHOMEDIR/config/azure-iot-edge-config.json```
* Modify the newly copied config file as needed. See the [Configuration File Description](#configuration-file-description) section for details related to the JSON keys and their values.
  * At the very least, users will need to setup these configuration items and use defaults for others.
    ```
    {
      "deviceConnectionString": "HostName=<>;DeviceId=<>;SharedAccessKey=<>",

      "homeDir": "<EDGEHOMEDIR>",

      "hostName": "<Host DNS Address>",

      ...
    }
    ```

### 3) Config File Setup
In this approach, users execute the azure-iot-edge-ctl script and supply the Edge configuration via option ```--config-file```. This is useful for experimentation,
development and test environments. For example a working configuration file
could be checked into a code repository and used by test automation scripts.

* Users should make a copy of this file ```EDGESCRIPTSDIR/config/azure-iot-edge-config-reference.json``` anywhere they like.
* Modify the newly copied config file as needed. See the [Configuration File Description](#configuration-file-description) section for details related to the JSON keys and their values.
  * At the very least, users will need to setup these configuration items and use defaults for others.
    ```
    {
      "deviceConnectionString": "HostName=<>;DeviceId=<>;SharedAccessKey=<>",

      "homeDir": "<EDGEHOMEDIR>",

      "hostName": "<Host DNS Address>",

      ...
    }
    ```
  * With this approach, the commands need to be invoked differently when using command start or restart:
  ```
    // Note: --config-file is ONLY needed for start and restart.
    azure-iot-edge-ctl start --config-file <path_to_file>
    azure-iot-edge-ctl restart --config-file <path_to_file>
    azure-iot-edge-ctl stop
    azure-iot-edge-ctl status
  ```
