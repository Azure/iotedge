# Module Management Agent
This project contains the module management agent.

## Application Settings.

The settings to run the MMA may be configured through the `appsettings.json` 
file or environment variables.

### appsettings.json

The application will read the `appsettings.json` for configuration. The format is:

```json
{
  "DockerUri": "<docker service uri>",
  "DeviceConnectionString": "<Your IoT Hub Device Connection String>",
  "ConfigSource": "<iothubconnected|standalone>",
  "DockerLoggingDriver":  "<json-file|journald|fluentd|etwlogs|none>",
  "NetworkId": "<Docker network id>",
  "EdgeDeviceHostName": "<Edge device host name>"
}
```

### Environment Variables

ALternatively, you may override the settings in `appsettings.json` by setting 
environment variables.

`DockerUri`

`DeviceConnectionString`

`ConfigSource`

`DockerLoggingDriver`

`NetworkId`

`EdgeDeviceHostName`

### DockerURI

This is the URI for the docker service.  Typically this is "http://localhost:2375"
on Windows and "unix:///var/run/docker.sock" on Linux.

### DeviceConnectionString

Set to the IoT Hub connection string of the edge device. Needed when 
`ConfigSource` is "iothubconnected".

### ConfigSource

May be set to "iothubconnected" or "standalone".  When set to "iothubconnected",
the edge device twin is used as a configuration source for modules. When set to 
standalone, a file (`config.json`) is used as a configuration source for modules. 
`DeviceConnectionString` must be set to a device connection string if `ConfigSource`
is set to "iothubconnected".

### DockerLoggingDriver

All modules created by MMA will use this setting to assign a logging driver for 
the Docker container.  See the 
[Docker logger driver documentation](https://docs.docker.com/engine/admin/logging/overview/#supported-logging-drivers)
for information on each log.  Drivers "json-file" (the default logger), and 
"journald" will be used by MMA.

Our current repository is set up to use the "json-file" option when MMA is 
running in Windows, and to use "journald" when running on a Linux target.  

#### Using "json-file"

The default option for the docker daemon is "json-file." The Docker service 
maintains one log per container. Logs are accessed through the `docker logs` 
command.  Logs are only available for containers that have not been removed.

#### Using "journald"

The journald logging system is available for all Linux system which support the 
systemd init system.

On the host machine, get all docker logs by running:
`journalctl --unit=docker`
Or get logs per module by running:
`journalctl -a CONTAINER_NAME=<module-name>`

Logs are not available through the `docker logs` command when using "journald."

#### Using other logging drivers

The following logging drivers have default options which should work when 
assigned: "fluentd", "etwlogs", and "none". No testing or validation was done on 
these logging drivers.

### NetworkId

Set to the Docker network you want the modules in the Edge deployment to be a part of.

### EdgeDeviceHostName

Set to the host name of the edge device, which will be used by the modules and 
downstream devices to connect to the EdgeHub.