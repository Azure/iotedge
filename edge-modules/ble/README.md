Bluetooth Low Energy Telemetry Sample for Azure IoT Edge Runtime
================================================================

Overview
--------

This Azure Edge sample showcases how one can take an existing IoT Gateway module that interacts with physical hardware, package it into a container and have it interact with the Edge runtime.
The sample chosen here is the Bluetooth Low Energy (BLE) module obtained from the following [repo]( https://github.com/Azure/azure-iot-gateway-sdk).

This sample contains:

  1. ***Dockerfile(s)*** for building a container image to host the BLE gateway module.
  2. ***build_ble_edge_module.sh*** a script to build and run the BLE module or simply pull down and run a prebuilt image.
    * Please run ```ble_edge_module.sh --help``` to view all the build and run options.
  3. ***ble_edge_module_sample.json*** under the config directory which contains the necessary configuration information needed to run the BLE gateway. A user would have to modify this file to suit their environment. Information on how to do this is listed below.

Prerequisites
-------------

1. The host OS would need to be setup per the recommendations made at the documents below:
  * [https://github.com/Azure/iot-edge-v1/blob/master/v1/doc/devbox_setup.md](https://github.com/Azure/iot-edge-v1/blob/master/v1/doc/devbox_setup.md)
  * [https://github.com/Azure/iot-edge-v1/tree/master/v1/samples/ble_gateway](https://github.com/Azure/iot-edge-v1/tree/master/v1/samples/ble_gateway)

2. Building and running this module will require ***root/sudo*** privileges.
    
3. Copy and/or modify the file ***config/ble_edge_module_sample.json*** and add the MAC address, IoT Hub details and device name and key. Details on how to fill these in are described in the repo links above.

Runtime and Security Considerations
-----------------------------------

  1. Ensure your host OS and BLE device are ready to run when the BLE module is run.
    * Please note that if you are running this on a Raspberry Pi you may need to unblock the Bluetooth radio by running  ``` sudo rfkill unblock bluetooth ```

  2. Do NOT add your configuration details and secrets such as the connection string, device key etc. in a configuration file within the container. Instead modify the config file on the host and have this volume mounted into your container at runtime. This permits docker image reuse and the image won't contain any credentials.

  3. Since DBUS is being used for communication with the BLE host driver, we volume mount the DBUS unix socket in the container. ```/var/run/dbus:/var/run/dbus```. This would almost always be the way to volume mount this resource, however if things are different on your platform, please adjust the runtime command as needed.

Steps to Build
--------------

You may either build the BLE docker image from source or use a prebuilt image from a docker container registry.
 
### Build Instructions
If you are interested in building (from source) and running the IoT Gateway in its container please run this command:

```
sudo ./ble_edge_module.sh --registry <registry> --ble_config_file <path_to_ble_module_config>
```

### Run Instructions

If you are interested in running a prebuilt image of the IoT Gateway in its container please run this command:

```
sudo ./ble_edge_module.sh --registry <registry> --ble_config_file <path_to_ble_module_config> --disable-ble-gateway-build --disable-docker-build
```

Additional build and run options are available, please run ```ble_edge_module.sh --help```.
