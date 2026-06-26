# Azure IoT Edge sample for configuration

This is a sample of using Azure IoT Direct Method to update configuration parameter in the IoT Edge module.

Temperature Simulator IoT Edge module sends simulated telemetry to the ConfigModule IoT Edge module at default frequency.

ConfigModule Module Direct Method can be invoked from IoT Hub. Once a new frequecy value is passed to edge via Module Direct Method, ConfigModule Direct Method handler will receive the new frequency value and update Temperature Simulator's telemetry sending frequency accordingly.

[Azure IoT Python SDK v2](https://github.com/Azure/azure-iot-sdk-python) is in use.




