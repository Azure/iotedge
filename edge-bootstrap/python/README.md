# Azure IoT Edge Runtime Control

## Introduction
The azure-iot-edge-runtime-ctl utility assists a user in managing and
controlling the Edge runtime.

Specifically, it can help in:
* Initial Bootstrap
* Certificate provisioning
* Starting/Stopping and other Edge runtime control options.

## Prerequisites:
    1. Linux/Windows
      * Python 2.7+
      * For Linux hosts user should have sudo capabilities.
    2. MacOS
      * TBD

## Installation:
    1. Copy the bootstrap utility directory anywhere on your filesystem.
      * Let's call this EDGEUTILSDIR.
    2. $> cd <EDGEUTILSDIR>
    3. sudo pip install -r requirements.txt

## How To Run:
The main commands to to operate the Edge runtime are:
setup, start, restart, stop, status and uninstall.

* **setup**: This command accepts users input to configure the runtime.
The required filesystem and certificates are created as part of this step.
Users can setup the Edge interactively or by using a input configuration file or
finally, specify the configuration items via command line args
Listed below are a few examples of how to use these.

* **start**: This command starts the Edge runtime. This downloads the Edge
Agent unless already available on the host machine. Any configuration information
pertaining to the host is supplied to the Edge Agent when it is instantiated.

* **restart**: This command restarts the Edge runtime and behaves like start
if the Edge Agent does not exist. If a runtime is active, it will be stopped and
started back to back.

* **stop**: This command stops the Edge runtime.

* **stop**: Prints the current state of the Edge runtime.

Print Help and Exit
```
$> python azure-iot-edge-runtime-ctl --help
```
Setup the Edge Runtime Using a Edge Host Configuration file.
Please see the [Edge Host Configuration File Description](#edge-host-configuration-file-description)
```
$> python azure-iot-edge-runtime-ctl setup --help
$> python azure-iot-edge-runtime-ctl setup --config-file edge_config.json
$> python azure-iot-edge-runtime-ctl setup --verbose DEBUG --config-file edge_config.json
Note: When specifying the homeDir path on Windows please use "C:\\example\\edge-home"
```

Setup the Edge Runtime Using Manually Specified Command Line Args
```
$> python azure-iot-edge-runtime-ctl setup --connection-string "HostName=<>;DeviceId=<>;SharedAccessKey=<>"
Note: When specifying the connection string ensure that it is surrounded by double quotes ".
In general anything with a semi colon should be puy into a separate command line args
```

Start the Edge Runtime
```
$> python azure-iot-edge-runtime-ctl start
$> python azure-iot-edge-runtime-ctl --verbose INFO start
```

Restart the Edge Runtime
```
$> python azure-iot-edge-runtime-ctl restart
$> python azure-iot-edge-runtime-ctl --verbose INFO restart
```

Stop the Edge Runtime
```
$> python azure-iot-edge-runtime-ctl stop
$> python azure-iot-edge-runtime-ctl --verbose INFO stop
```

Uninstall the Edge Runtime
```
$> python azure-iot-edge-runtime-ctl uninstall
$> python azure-iot-edge-runtime-ctl --verbose DEBUG uninstall
```

Print the current status of the Edge Runtime
```
$> python azure-iot-edge-runtime-ctl status
$> python azure-iot-edge-runtime-ctl --verbose INFO status
```

### Edge Home Directory Description
The Edge runtime needs a directory on the host machine in order to execute.
This directory will contain the necessary configuration, certificates and
module specific files. Lets call this the *EDGEHOMEDIR*. If users do not specify
a value for the *EDGEHOMEDIR*, these default directories will be used to
setup/start/stop the Edge runtime.

```
Default Host Paths:
-------------------
    Linux:   /usr/local/azure-iot-edge
    Windows: C:\azure-iot-edge
    MacOS:   TBD
```

As the Edge runtime is executed, the following file system structure
is created under *EDGEHOMEDIR*.

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

The following section goes into details of the various configuration items
and lays out how users are expected to modify this.

```
  // Config file format schema; Users should not need to modify this.
  "schemaVersion": "1",

  // User's IoTHub Device Connection string in the format listed below.
  "deviceConnectionString": "HostName=<>;DeviceId=<>;SharedAccessKey=<>",

  // Path to the Edge home dir, if left empty, a default home dir will be used
  "homeDir": "<EDGEHOMEDIR>",

  // Edge device's DNS name;
  // Specifying a FQDN is only required when operating the
  // Edge as a 'Gateway' for leaf device connectivity.
  // If a FQDN is unavailable, the host name could be used. If left blank,
  // the utility will determine the FQDN if available or the machine name.
  // This hostname value is needed for certificate
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
        "forceRegenerate": false,
        "forceNoPasswords": true
      },

      "preInstalled": {
        "deviceCACertificateFilePath": "",
        "serverCertificateFilePath": ""
      }
    }
  },
  // Section containing Configuration of Edge Runtime Deployment and Host.
  "deployment": {

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
