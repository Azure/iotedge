
# Tracking $edgeAgent's reported properties during deployment.

An IoT Edge runtime receives a new deployment through $edgeAgent's twin in the desired properties 
section.  When it receives this new deployment, it creates a plan to apply changes to the current 
system. Once that plan finishes executing, successfully or not, the $edgeAgent will update its 
reported properties.  

This document will help spell out some of the details of $edgeAgent's twin properties as the IoT 
Edge runtime applies a new deployment. 

# Relevant twin properties

There are two sections describing relevant twin properties below. The first describes properties 
set on the Iot Hub service and read by the IoT Edge. The second describes properties set by the IoT Edge
and reported back to the service.

## Set by IoT Hub service

The following sections are set in edgeAgent twin by the IoT Hub service and are read by the IoT Edge 
runtime:

### Configuration section

This section is present when the service applies at-scale (base and layered) deployments. This 
section will not be present if no at-scale or layered deployment is applied.

Example:

```json
{
    "configurations" : {
        "at-scale1": {
            "status": "Applied"
        },
        "at-scale2": {
            "status": "Targeted"
        }
    }
}
```

| Relevant Field | Meaning |
|------------------------------------------|-------------------------------------------------------|
| .configurations.*\<deployment name\>*.status | "Applied" or "Targeted" for each deployment that may be targeted to this device. |


### Desired Properties section

This is the $edgeAgent section of the modulesContent in an edge deployment.

Example:

```json
{
    "properties": {
        "desired": {
            "modules": {
                "m1": {
                    "settings": {
                        "image": "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0",
                        "createOptions": ""
                    },
                    "type": "docker",
                    "status": "running",
                    "restartPolicy": "always",
                    "version": "1.0"
                }
            },
            "runtime": {
                "settings": {
                    "minDockerVersion": "v1.25"
                },
                "type": "docker"
            },
            "schemaVersion": "1.1",
            "systemModules": {
                "edgeAgent": {
                    "settings": {
                        "image": "mcr.microsoft.com/azureiotedge-agent:1.1",
                        "createOptions": ""
                    },
                    "type": "docker"
                },
                "edgeHub": {
                    "settings": {
                        "image": "mcr.microsoft.com/azureiotedge-hub:1.1",
                        "createOptions": "{\"HostConfig\":{\"PortBindings\":{\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}],\"8883/tcp\":[{\"HostPort\":\"8883\"}]}}}"
                    },
                    "type": "docker",
                    "status": "running",
                    "restartPolicy": "always"
                }
            },
            "$version": 11
        }
    }
}
```
 
| Relevant Field | Meaning |
|------------------------------------------|-------------------------------------------------------|
| .properties.desired."$version"           | This number is monotonically increasing. Even a rollback will increase this number. |
| .properties.desired.systemModules.edgeAgent.settings.image | $edgeAgent’s desired image tag. |
| .properties.desired.systemModules.edgeHub.status | $edgeHub’s desire status, “running” or “stopped”. |
| .properties.desired.systemModules.edgeHub.settings.image | $edgeHub’s desired image tag. |
| .properties.desired.modules.*\<module\>*.status | A module’s desired status, “running” or “stopped”. |
| .properties.desired.modules.*\<module\>*.settings.image | A module’s desired image tag. |


## Set by IoT Edge

These properties are set on the IoT Edge and reported back to the service via $edgeAgent Twin.

### Reported Properties section

Example:
```json
{
    "properties" : {
        "reported": {
            "lastDesiredVersion": 11,
            "lastDesiredStatus": {
                "code": 406,
                "description": "Agent is not running"
            },
            "schemaVersion": "1.0",
            "version": {
                "version": "1.1.3",
                "build": "42832849",
                "commit": "68e71a5384b14956775241557877497274d0ce7e"
            },
            "runtime": {
                "platform": {
                    "os": "linux",
                    "architecture": "x86_64",
                    "version": "1.1.3"
                },
                "type": "docker",
                "settings": {
                    "minDockerVersion": "v1.25",
                    "registryCredentials": {}
                }
            },
            "systemModules": {
                "edgeHub": {
                    "type": "docker",
                    "status": "running",
                    "restartPolicy": "always",
                    "exitCode": 0,
                    "statusDescription": "running",
                    "lastStartTimeUtc": "2021-06-12T00:27:46.3838704",
                    "lastExitTimeUtc": "2021-06-12T00:26:39.2078664",
                    "restartCount": 0,
                    "lastRestartTimeUtc": "2021-06-12T00:26:39.2078664",
                    "runtimeStatus": "unknown",
                    "settings": {
                        "image": "mcr.microsoft.com/azureiotedge-hub:1.1",
                        "imageHash": "sha256:dd0d4897a65fae460909e497386606b5cd7a2ad7ae0605f5bb766644e352fac3",
                        "createOptions": "{\"HostConfig\":{\"PortBindings\":{\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}],\"8883/tcp\":[{\"HostPort\":\"8883\"}]}}}"
                    }
                },
                "edgeAgent": {
                    "type": "docker",
                    "startupOrder": 0,
                    "exitCode": 0,
                    "statusDescription": "running",
                    "lastStartTimeUtc": "2021-06-12T00:27:35.4200863",
                    "lastExitTimeUtc": "2021-06-12T00:26:39.6337272",
                    "runtimeStatus": "unknown",
                    "imagePullPolicy": "on-create",
                    "settings": {
                        "image": "mcr.microsoft.com/azureiotedge-agent:1.1",
                        "imageHash": "sha256:f0eda4eb5fd4aecbc0d260cf9e31eb88d58db9483fc2d54425e820c94fe6dd08",
                        "createOptions": "{\"Labels\":{\"net.azure-devices.edge.create-options\":\"{}\",\"net.azure-devices.edge.env\":\"{}\",\"net.azure-devices.edge.owner\":\"Microsoft.Azure.Devices.Edge.Agent\"}}"
                    }
                }
            },
            "modules": {
                "m1": {
                    "exitCode": 0,
                    "statusDescription": "running",
                    "lastStartTimeUtc": "2021-06-12T00:27:43.9269859",
                    "lastExitTimeUtc": "2021-06-12T00:26:29.1405344",
                    "restartCount": 0,
                    "lastRestartTimeUtc": "2021-06-12T00:26:29.1405344",
                    "runtimeStatus": "unknown",
                    "version": "1.0",
                    "status": "running",
                    "restartPolicy": "always",
                    "imagePullPolicy": "on-create",
                    "type": "docker",
                    "settings": {
                        "image": "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0",
                        "imageHash": "sha256:85fdb1e9675c837c18b75f103be6f156587d1058eced1fc508cdb84a722e4f82",
                        "createOptions": "{}"
                    },
                    "env": {}
                }
            },
            "$version": 196
        }
    }
}
```

| Relevant Field | Meaning |
|------------------------------------------|-------------------------------------------------------|
| .properties.reported.lastDesiredVerison | The last version number received and acted on. |
| .properties.reported.lastDesiredStatus.code | The current status of the deployment (see table below).   |
| .properties.reported.lastDesiredStatus.description | Text describing the deployment status. |
| .properties.reported.systemModules.edgeAgent.runtimeStatus | See module status table below. |
| .properties.reported.systemModules.edgeAgent.settings.image | Current image being run by container runtime. | 
| .properties.reported.systemModules.edgeHub.runtimeStatus | See module status table below. |
| .properties.reported.systemModules.edgeHub.settings.image | Current image being run by container runtime. |
| .properties.reported.modules.*\<module\>*.runtimeStatus | See module status table below. |
| .properties.reported.modules.*\<module\>*.settings.image | Current image being run by container runtime. |

A module's runtime status is the current status of the modules as interpreted by the IoT Edge 
runtime from the container status.  The image names are read from the container itself, so will 
reflect the currently running image.

#### Last Desired Status codes.

| Code | Meaning |
|------|---------|
| 200  | Successful deployment |
| 400  | ConfigFormatError - deployment was not able to be read by edgeAgent |
| 406  | Unknown |
| 412  | InvalidSchemaVersion - deployment schema version not compatible with edgeAgent |
| 417  | ConfigEmptyError - no deployment has been send to IoT Edge |
| 500  | Failed - something has gone wrong when applying a deployment |

The code will be 406(Unknown) until the edgeAgent is able to connect to the IoT Hub service, and 
will be set to 406 when edgeAgent shuts down.

Once online, the code may be 417(ConfigEmptyError) until the edgeAgemt receives a non-empty 
deployment.

Once the deployment is successfully realized, the code will be 200(Ok).

If the deployment is not successfully realized, the code will either be 400, 412, 417 or 500, based on the
reason why it failed.

#### Module and system module runtime status

| status  | Meaning |
|---------|---------|
| unknown | Module status is unknown. |
| backoff | Modules transition to the backoff state when the edgeAgent has scheduled the module to be started but hasn't actually started running yet.|
| running | Module is currentlt running. |
| stopped | Module has exited successfully (with a zero exit code). |
| failed  | Module has exited with a failure exit code (non-zero). |

# How these fields relate when applying a deployment

The following section shows how you might detect the status of an IoT Edge Deployment, based on the fields defined above

### Before IoT Edge applies current deployment

When the twin fields are in this state, the IoT Hub has applied a deployment, but the IoT Edge has
not received and processed it.

```
configurations.<deployment name>.Status: "Applied" && 
.properties.desired."$version" != .properties.reported.lastDesiredVerison 
```
 

### Completely successful

A deployment may report a 200 code, which means it successfully deployed all modules, and all modules
have come into the correct state.


```
configurations.<deployment name>.Status: "Applied" && 

.properties.desired."$version" == .properties.reported.lastDesiredVerison && 

.properties.reported.lastDesiredStatus.code == 200 && 
```

For each desired module:
```
   .properties.desired.modules.<module>.status == .properties.reported.modules.<module>.runtimeStatus &&  

   .properties.desired.modules.<module>.settings.image == .properties.reported.modules.<module>.settings.image 
```
 

### Deployment is invalid

The IoT Edge runtime has received a deployment, but the deployment was invalid.

```
configurations.<deployment name>.Status: "Applied" && 

.properties.desired."$version" == .properties.reported.lastDesiredVerison && 

.properties.reported.lastDesiredStatus.code == 400 or 412  
```

NOTE: We wouldn’t expect to see “406” unless edgeAgent shuts down, or “417” (empty deployment) at 
all once the first deployment is applied. These may occur if the edgeAgent is being updated.


### Deployment succeeded, module failed after launch. 

In this state, the IoT Edge runtime has successfuly deployed all modules, but the modules went into
a incorrect state after being deployed.

```
configurations.<deployment name>.Status: "Applied" && 

.properties.desired."$version" == .properties.reported.lastDesiredVerison && 

.properties.reported.lastDesiredStatus.code == 200 && 
```

For any desired module:
```
   .properties.desired.modules.<module>.status  != .properties.reported.modules.<module>.runtimeStatus &&  

   .properties.desired.modules.<module>.settings.image == .properties.reported.modules.<module>.settings.image 
```

### Deployment in progress/Deployment failed.

One of the steps that the IoT Edge runtime attempted to aplly the current desired state failed. 
There may be many reasons for this, such as an image pull request is taking too long, the module 
specification references a bad image, or the container runtime failed for any number of reasons.


```
configurations.<deployment name>.Status: "Applied" && 

.properties.desired."$version" == .properties.reported.lastDesiredVerison && 

.properties.reported.lastDesiredStatus.code == 500 && 
```

For any desired module:   
```
   .properties.desired.modules.<module>.settings.image != .properties.reported.modules.<module>.settings.image 
```

NOTE: if this is a new module in the deployment, it will not exist in the reported properties until 
the module is created.

NOTE: If the module was previously marked as “running” it should stay “running” using the previous 
image until the pull for the new module is complete.

NOTE: The IoT Edge runtime doesn’t have a clear indicator that the pull is in progress compared to 
the pull failing for some other reason (like a bad image name).  You may be able to derive some 
information from `.properties.reported.lastDesiredStatus.description`.  For example, if the pull 
was timed out, the text will contain “The operation was canceled”, and this state is probably 
transitional.

