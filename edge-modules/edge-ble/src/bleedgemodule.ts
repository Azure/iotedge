import {
    Client as DeviceClient,
    DeviceMethodRequest,
    DeviceMethodResponse,
    Message
} from "azure-iot-device";

import { Mqtt as DeviceTransport } from "azure-iot-device-mqtt";

import {
    ISensorManager,
    ISensorHub,
    ISensorDevice,
    SensorDeviceMessage
} from "./blemodule";

interface MethodHandler {
    callback: (request: DeviceMethodRequest, response: DeviceMethodResponse) => Promise<any>;
    requiredKeys: string[];
}

interface ConfigState {
    configurationVersion: string;
    status: string;
}

class ModuleConfigStatus {
    public static readonly error = "Error";
    public static readonly notReady = "Not Ready";
    public static readonly pendingConfigUpdate = "Pending Configuration Update";
    public static readonly ready = "Ready";
}

export class BLEEdgeModule {
    private static readonly executeSensorCommandRequiredKeys = ["sensorHub", "commandId", "device"];
    private static readonly executeSensorHubCommandRequiredKeys = ["sensorHub", "commandId"];
    private static readonly twinUpdateTimeoutInMs = 10000;
    private static readonly sensorManagerInitTimeoutInMs = 20000;
    private client: DeviceClient;
    private state: string;
    private twin: any;
    private sensorManager: ISensorManager;
    private readonly sensorConfigFilePath: string;
    private methodHandlers: { [methodName: string]: MethodHandler; };
    private currentConfigurationState: ConfigState;

    constructor(connectionString: string, sensorManager: ISensorManager, filePath: string) {
        this.sensorManager = sensorManager;
        this.sensorConfigFilePath = filePath;

        // init module state
        this.state = "uninit";
        this.currentConfigurationState = { configurationVersion: "",
                                           status: ModuleConfigStatus.notReady };

        // init client
        this.client = DeviceClient.fromConnectionString(connectionString, DeviceTransport);

        // register method handlers
        this.methodHandlers = {};
        const methodHandlerDevice: MethodHandler = { callback: this.executeSensorDeviceCommand,
                                                   requiredKeys: ["sensorHub", "commandId", "device"] };
        const methodHandlerHub: MethodHandler = { callback: this.executeSensorHubCommand,
                                                requiredKeys: ["sensorHub", "commandId"] };
        this.methodHandlers.executeSensorCommand = methodHandlerDevice;
        this.methodHandlers.executeSensorHubCommand = methodHandlerHub;
        this.client.onDeviceMethod("executeSensorCommand", this.onDeviceMethodHandler);
        this.client.onDeviceMethod("executeSensorHubCommand", this.onDeviceMethodHandler);
    }

    public async start(): Promise<void> {
        return new Promise<void>(async (resolve, reject) => {
            if (this.state === "started") {
                resolve();
            } else if (this.state === "uninit") {
                try {
                    await this.sensorManager.initialize(BLEEdgeModule.sensorManagerInitTimeoutInMs);
                    await this.sensorManager.updateSensorConfigurationFromFile(this.sensorConfigFilePath);
                    console.info("Initialized Sensor Manager");
                    await this.sensorManager.printConfiguration();

                    await this.controlDevices(true);
                    this.currentConfigurationState.status = ModuleConfigStatus.ready;
                    this.currentConfigurationState.configurationVersion =
                        this.sensorManager.getConfigurationVersion().toString();

                    await this.open();
                    console.info("IoT Hub Connection Established");

                    this.state = "started";
                    this.client.getTwin(this.initializeTwin);
                    resolve();
                } catch (err) {
                    console.error("Module Init Failed " + err);
                    this.currentConfigurationState.status = ModuleConfigStatus.error;
                    reject(err);
                }
            } else {
                reject(new Error("Module Cannot Be Started"));
            }
        });
    }

    public stop(): void {
        if (this.state === "started") {
            this.state = "stopped";
            this.controlDevices(false);
            this.client.close();
        }
    }

    private async open(): Promise<void> {
        return new Promise<void>((resolve, reject) => {
            this.client.open((err: any) => {
                if (err) {
                    console.error(err.toString());
                    reject(err);
                } else {
                    resolve();
                }
            });
        });
    }

    private initializeTwin = (err: any, twin: any) => {
        if (err) {
            console.error("Could not get Twin");
        } else {
            this.twin = twin;
            this.updateTwinReportedState();
            twin.on("properties.desired", this.onTwinDesiredUpdate);
        }
    }

    private updateTwinReportedState() {
        const configuration = {
            status: {
                status: this.currentConfigurationState.status,
                version: this.currentConfigurationState.configurationVersion
            }
        };

        this.twin.properties.reported.update(configuration, (err: any) => {
            if (err) {
                console.error("Could not update Reported Twin State");
            } else {
                console.debug("Updated Reported Twin State");
            }
        });
    }

    private onTwinDesiredUpdate = (desiredChange: any): void => {
        const current = this.twin.properties.reported.configuration;
        if (desiredChange && desiredChange.configuration &&
            desiredChange.configuration.version !== this.currentConfigurationState.configurationVersion) {
            console.info("Received twin change: " + JSON.stringify(desiredChange));
            setTimeout(() => {
                this.onUpdateConfiguration(desiredChange.configuration);
            }, BLEEdgeModule.twinUpdateTimeoutInMs);
            this.currentConfigurationState.status = ModuleConfigStatus.pendingConfigUpdate;
            this.updateTwinReportedState();
        }
    }

    private validateCommandRequest(request: DeviceMethodRequest, reqKeys: string[]): boolean {
        let result = false;
        if (request) {
            result = true;
            for (let idx = 0; idx < reqKeys.length; idx++) {
                if (!(reqKeys[idx] in request.payload)) {
                    result = false;
                    break;
                }
            }
        }
        return result;
    }

    private executeSensorHubCommand = async (request: DeviceMethodRequest,
                                                response: DeviceMethodResponse): Promise<void> => {
        return new Promise<void>(async (resolve, reject) => {
            const commandId = request.payload.commandId;
            const commandData = (request.payload.commandData) ? request.payload.commandData : null;
            const sensorHubId = request.payload.sensorHub;

            const sensor = this.sensorManager.getSensorHubById(sensorHubId);
            if (sensor == null) {
                reject("Invalid Sensor Hub " + sensorHubId);
            } else {
                try {
                    await sensor.executeCommand(commandId, commandData);
                    resolve();
                } catch (err) {
                    console.error("Could Not Execute Sensor Hub Command");
                    reject(err);
                }
            }
        });
    }

    private executeSensorDeviceCommand = async (request: DeviceMethodRequest,
                                                    response: DeviceMethodResponse): Promise<void> => {
        return new Promise<void>(async (resolve, reject) => {
            const sensor = this.sensorManager.getSensorHubById(request.payload.sensorHub);
            if (sensor == null) {
                reject("Invalid Sensor Hub " + request.payload.sensorHub);
            } else {
                const device = sensor.getSensorDeviceById(request.payload.device);
                if (device == null) {
                    reject("Invalid Sensor Hub Device " + request.payload.device);
                } else {
                    try {
                        const commandId = request.payload.commandId;
                        const commandData = (request.payload.commandData) ? request.payload.commandData : null;
                        const data: Buffer = await device.executeCommand(commandId, commandData);
                        if (data) {
                            response.payload = data.toString("hex");
                        }
                        resolve();
                    } catch (deviceErr) {
                        reject(deviceErr);
                    }
                }
            }
        });
    }

    private onDeviceMethodHandler = async (request: DeviceMethodRequest,
                                      response: DeviceMethodResponse) => {
        let respMsg: string;
        let respCode: number;

        console.info("Request MethodName " + request.methodName +
                        " Payload " + JSON.stringify(request.payload) +
                        " RequestId " + request.requestId);
        const methodName = request.methodName;
        if (methodName in this.methodHandlers) {
            const methodHandler = this.methodHandlers[methodName];
            if (!this.validateCommandRequest(request, methodHandler.requiredKeys)) {
                respMsg = "Malformed Execute Command Payload";
                respCode = 400;
            } else {
                try {
                    respMsg = await methodHandler.callback(request, response);
                    respCode = 200;
                } catch (err) {
                    respMsg = "Command Failed to Execute";
                    respCode = 500;
                }
            }
        } else {
            respMsg = "Unknown Device Method";
            respCode = 400;
        }

        response.send(respCode, respMsg, (err: any) => {
            if (err) {
                console.error("An error ocurred when sending a method response:\n" + err.toString());
            } else {
                console.info("Response to method '" + request.methodName + "\ sent successfully.");
            }
        });
    }

    private onUpdateConfiguration = async (newConfig: any): Promise<void> => {
        console.info("New Configuration: " + JSON.stringify(newConfig) +
                     " Current Version: " + this.currentConfigurationState.configurationVersion);
        return new Promise<void>(async (resolve, reject) => {
            try {
                if (newConfig.version !==
                        this.currentConfigurationState.configurationVersion) {
                    console.info("Removing Any Active Devices");
                    await this.controlDevices(false);

                    console.info("Updating New Configuration");
                    await this.sensorManager.updateSensorConfigurationFromJSON(newConfig);
                    await this.sensorManager.printConfiguration();
                    console.info("Updating New Configuration Done");

                    console.info("Reconnecting New Active Devices");
                    await this.controlDevices(true);
                    console.info("Reconnecting New Active Devices Done");

                    this.currentConfigurationState.configurationVersion =
                        this.sensorManager.getConfigurationVersion().toString();
                    this.currentConfigurationState.status = ModuleConfigStatus.ready;
                }
                resolve();
            } catch (err) {
                this.currentConfigurationState.status = ModuleConfigStatus.error;
                console.error("Could Not Update Configuration");
                reject(err);
            } finally {
                this.updateTwinReportedState();
                console.info("Updating Twin State Done");
            }
        });
    }

    private controlDevices = async (connectToDevices: boolean): Promise<void> => {
        return new Promise<void>(async (resolve) => {
            if (connectToDevices) {
                const sensorHubIds = this.sensorManager.getConfiguredSensorHubIds();
                for (const sensorHubId of sensorHubIds) {
                    try {
                        const sensorHub: ISensorHub = await this.sensorManager.connectToSensorHubById(sensorHubId);
                        const devices = sensorHub.getSensorDevices();
                        devices.forEach((device) => {
                            device.on("reportIntervalData", this.onSendMessage);
                        });
                    } catch (err) {
                        console.error("Could Not Connect To Sensor Hub " + sensorHubId);
                    }
                }
                resolve();
            } else {
                const connectedSensorHubs = this.sensorManager.getConnectedSensorHubs();
                connectedSensorHubs.forEach(async (sensorHub) => {
                    const devices = sensorHub.getSensorDevices();
                    devices.forEach((device) => {
                        device.removeListener("reportIntervalData", this.onSendMessage);
                    });
                });
                resolve();
            }
        });
    }

    private onSendMessage = async (message: SensorDeviceMessage) => {
        const msgBody = JSON.stringify(message.body);
        console.info("Sending Telemetry Data From Device " + msgBody);
        const msg = new Message(msgBody);

        if ((msg.properties) && (message.properties)) {
            for (const key in msg.properties) {
                if (msg.properties.hasOwnProperty(key)) {
                    msg.properties.add(key, message.properties[key]);
                }
            }
        }

        this.client.sendEvent(msg, (err: any) => {
            if (err) {
                console.error("Could Not Send Message to IoT Hub With Id");
            }
        });
    }
}
