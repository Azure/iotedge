# How to configure module startup order

By default, IoT Edge does not impose an ordering in the sequence in which modules are started, updated or stopped. Edge Agent by default is the first module that gets started and based on the edge deployment specification, it figures out which modules need to be started, updated or stopped and executes those operations in a non-deterministic order.

The processing order of modules can be controlled by specifying the value of a module-specific property called `startupOrder` in the IoT Edge deployment. Modules that have been assigned a higher priority will be processed before modules that have been assigned a lower priority.

## __Use case__

Customers who have an array of modules of which some are 'critical' or 'foundation' modules that are required by other modules in the ecosystem might want these modules to be started before other modules. This is so as to achieve a better end user experience where other modules don't have to wait for these 'critical' or 'foundation' modules to be started, so as to initialize themselves.

As an example, some customers want the Edge Hub module to be started before any other non-system modules in the ecosystem are started. This is so that other modules don't spend unnecessary cycles waiting for Edge Hub to come up before they can start sending messages to other modules or upstream to IoT Hub.

**That being said, module owners should design their modules to withstand any failures of these 'critical' or 'foundation' modules, that they are dependent upon, as they could go down at any arbitrary time and an arbitrary number of times.**

## __Configuration__

Customers can optionally specify a `startupOrder` value for each module in their IoT Edge deployment to achieve module boot ordering. Modules with a higher priority will be created and started first and only when an attempt has been made to start them, will other lower priority modules be created and started.

The value of `startupOrder` will be positive and zero-based with 0 being the highest priority value.
A higher numeric `startupOrder` value would indicate a lower priority assigned to that module. The maximum value of this property will be 4294967295.
Modules that possess the same priority will be created at the same time and will have no deterministic startup order imposed amongst themselves. Modules with higher priority values will be created and started first and once Edge Agent has made an attempt to create and start them (only the modules with a desired state of `Running` will be started), it will proceed with the creation (and optionally startup) of other lower priority modules in order.

**It must be noted that the Edge Agent module will not support the `startupOrder` property and will by default have the highest priority. As is the behavior currently, Edge Agent will always be the first module to get started.**

Modules that do not have a `startupOrder` value specified will be started in a non-deterministic order and will be assigned a priority of 4294967295 which indicates the lowest possible priority. Edge Agent will trigger the creation of these modules after all modules with a higher priority have been created.

**Please note that Kubernetes mode of IoT Edge does not support module startup order priorities.**

## __Example__

### __How to set priorities of Edge modules__

Here's an example of how to set the priorities of IoT Edge modules through Az CLI:

Create a deployment manifest `deployment.json` JSON file that has your IoT Edge deployment specification. Please refer to [Learn how to deploy modules and establish routes in IoT Edge][1] for more information about the IoT Edge deployment manifest.

The following sample deployment manifest illustrates how priority values of modules can be set:

```JSON
{
  "modulesContent": {
    "$edgeAgent": {
      "properties.desired": {
        "schemaVersion": "1.0",
        "runtime": {
          "type": "docker",
          "settings": {
            "minDockerVersion": "v1.25",
            "loggingOptions": "",
            "registryCredentials": {
              "ContosoRegistry": {
                "username": "myacr",
                "password": "<password>",
                "address": "myacr.azurecr.io"
              }
            }
          }
        },
        "systemModules": {
          "edgeAgent": {
            "type": "docker",
            "settings": {
              "image": "mcr.microsoft.com/azureiotedge-agent:1.0",
              "createOptions": ""
            }
          },
          "edgeHub": {
            "type": "docker",
            "status": "running",
            "restartPolicy": "always",
            "settings": {
              "image": "mcr.microsoft.com/azureiotedge-hub:1.0",
              "createOptions": ""
            },
            "startupOrder": 0
          }
        },
        "modules": {
          "SimulatedTemperatureSensor": {
            "version": "1.0",
            "type": "docker",
            "status": "running",
            "restartPolicy": "always",
            "settings": {
              "image": "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0",
              "createOptions": "{}"
            },
            "startupOrder": 1
          },
          "filtermodule": {
            "version": "1.0",
            "type": "docker",
            "status": "running",
            "restartPolicy": "always",
            "settings": {
              "image": "myacr.azurecr.io/filtermodule:latest",
              "createOptions": "{}"
            }
          }
        }
      }
    },
    "$edgeHub": {
      "properties.desired": {
        "schemaVersion": "1.0",
        "routes": {
          "sensorToFilter": "FROM /messages/modules/SimulatedTemperatureSensor/outputs/temperatureOutput INTO BrokeredEndpoint(\"/modules/filtermodule/inputs/input1\")",
          "filterToIoTHub": "FROM /messages/modules/filtermodule/outputs/output1 INTO $upstream"
        },
        "storeAndForwardConfiguration": {
          "timeToLiveSecs": 10
        }
      }
    }
  }
}
```

In the sample deployment manifest shown above:

* The `$edgeHub` module has been assigned a `startupOrder` value of 0.
* The `SimulatedTemperatureSensor` module has been assigned a `startupOrder` value of 1.
* The `filtermodule` module has not been assigned any `startupOrder` value which means that it will by default assume the priority of 4294967295 which is the lowest priority value possible.

When this deployment manifest is deployed to a device that does not have any modules running, `$edgeHub` is the first module that will come up followed by the `SimulatedTemperatureSensor` module and then the `filtermodule`.

Please refer to [Deploy Azure IoT Edge modules with Azure CLI][2] for steps on how to deploy the deployment.json file to your device.

[1]: https://docs.microsoft.com/azure/iot-edge/module-composition
[2]: https://docs.microsoft.com/en-us/azure/iot-edge/how-to-deploy-modules-cli