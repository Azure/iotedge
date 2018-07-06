# IoT Edge

Welcome to the home of IoT Edge.

IoT Edge moves cloud analytics and custom business logic to devices so that your organization can focus on business insights instead of data management.
Enable your solution to truly scale by configuring your IoT software, deploying it to devices via standard containers, and monitoring it all from the cloud.
This repository consists of three main projects: the [Edge Agent](edge-agent), the [Edge Hub](edge-hub), and the [IoT Edge Security Daemon](edgelet).

## Documentation
Documentation for the Azure IoT Edge product can be found at https://docs.microsoft.com/azure/iot-edge.

## Featured Modules
The following modules are available in this repository:
>| Name                     | Description                     | Project Link                                                                                          |
>| ------------------------ | ------------------------------- | ----------------------------------------------------------------------------------------------------- |
>| Simulated Temp Sensor    | Simulated C# Temperature Sensor | [Project Link](https://github.com/Azure/iotedge/tree/master/edge-modules/SimulatedTemperatureSensor)  |
>| Temperature Filter       | Simple Value Filter Module      | [Project Link](https://github.com/Azure/iotedge/tree/master/edge-modules/TemperatureFilter)           |
>| BLE                      | Bluetooth Low Eneregy           | [Project Link](https://github.com/Azure/iotedge/tree/master/edge-modules/ble)                         |
>| Functions                | Azure Functions on Edge         | [Project Link](https://github.com/Azure/iotedge/tree/master/edge-modules/functions)                   |
>| Node Sensor              | Simulated Node Temp Sensor      | [Project Link](https://github.com/Azure/iotedge/tree/master/edge-modules/node-sensor-sample)          | 

## Community Modules
Other people are creating modules for Azure IoT Edge too! See the Project Link for a module to find out how to get it, who supports it, etc.
>| Name             | Project Link                                                            |
>| ---------------- | ----------------------------------------------------------------------- |
>| OPC Publisher    | [Project Link](https://github.com/Azure/iot-edge-opc-publisher)         |
>| OPC Proxy        | [Project Link](https://github.com/Azure/iot-edge-opc-proxy)             |
>| Modbus           | [Project Link](https://github.com/Azure/iot-edge-modbus)                |
>| Darknet          | [Project Link](https://github.com/vjrantal/iot-edge-darknet-module)     |
>| General Logging  | [Project Link](https://github.com/ytechie/IoTEdgeLoggingModule)         |
>| Log Analytics    | [Project Link](https://github.com/veyalla/logspout-loganalytics)        | 


## Contributing

If you would like to build or change the IoT Edge source code, please follow the [devguide](doc/devguide.md).


---
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
