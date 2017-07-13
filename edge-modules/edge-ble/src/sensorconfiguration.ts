import * as fs from "async-file";

import {
    CommandType,
    IDeviceCommand,
    IDeviceConfiguration,
    ISensorHubConfiguration
} from "./blemodule";

class DeviceCommand implements IDeviceCommand {
    public readonly id: string;
    public readonly type: CommandType;
    public readonly uuid: string;
    public readonly writeData: string;
    public readonly defaultIntervalInMs: number;

    constructor(id: string,
                type: CommandType,
                uuid: string,
                writeData: string,
                defaultIntervalInMs: number) {
        this.id = id;
        this.type = type;
        this.uuid = uuid;
        this.writeData = writeData;
        this.defaultIntervalInMs = defaultIntervalInMs;
    }

    public toString() {
        return `{${this.id}, ${this.type}, "${this.uuid}", "${this.writeData}", ${this.defaultIntervalInMs}}`;
    }
}

class DeviceConfiguration extends IDeviceConfiguration {
    private readonly id: string;
    private readonly name: string;
    private readonly serviceUUID: string;
    private readonly commands: { [id: string]: IDeviceCommand; };

    constructor(id: string,
                name: string,
                serviceUUID: string,
                deviceCommands: IDeviceCommand[]) {
        super();
        this.id = id;
        this.name = name;
        this.serviceUUID = serviceUUID.toLowerCase();
        this.commands = {};
        for (const command of deviceCommands) {
            this.commands[command.id] = command;
        }
    }

    public reset() {
        for (const key in this.commands) {
            if (this.commands.hasOwnProperty(key)) {
                delete this.commands[key];
            }
        }
    }

    public toString() {
        return `{${this.id}, ${this.name}, ${this.serviceUUID}}`;
    }

    public getId(): string {
        return this.id;
    }

    public getName(): string {
        return this.name;
    }

    public getServiceUUID(): string {
        return this.serviceUUID;
    }

    public getCommands(): IDeviceCommand[] {
        return Object.keys(this.commands).map((key) => {
            return this.commands[key];
        });
    }

    public getCommandById(id: string): IDeviceCommand {
        return (id in this.commands) ? this.commands[id] : null;
    }

    public getCommandByUUID(characteristicUUID: string): IDeviceCommand {
        characteristicUUID = characteristicUUID.toLowerCase();
        for (const configKey in this.commands) {
            if (this.commands[configKey].uuid === characteristicUUID) {
                return this.commands[configKey];
            }
        }
        return null;
    }
}

class SensorHubConfiguration extends ISensorHubConfiguration {
    private readonly id: string;
    private readonly name: string;
    private readonly uuid: string;
    private readonly sensorDevicesConfig: { [id: string]: DeviceConfiguration; };

    constructor(id: string,
                name: string,
                uuid: string,
                devices: DeviceConfiguration[]) {
        super();
        this.id = id;
        this.name = name;
        this.uuid = uuid || "";
        this.sensorDevicesConfig = {};
        for (const device of devices) {
            this.sensorDevicesConfig[device.getId()] = device;
        }
    }

    public reset() {
        for (const key in this.sensorDevicesConfig) {
            if (this.sensorDevicesConfig.hasOwnProperty(key)) {
                this.sensorDevicesConfig[key].reset();
                delete this.sensorDevicesConfig[key];
            }
        }
    }

    public getId(): string {
        return this.id;
    }

    public getName(): string {
        return this.name;
    }

    public getUUID(): string {
        return this.uuid;
    }

    public toString() {
        return `{${this.id}, ${this.name}, ${this.uuid}}`;
    }

    public getDevicesConfiguration(): IDeviceConfiguration[] {
        const devices = this.sensorDevicesConfig;
        return Object.keys(devices).map((key) => { return devices[key]; });
    }

    public getDeviceConfigurationById(id: string): IDeviceConfiguration {
        return (id in this.sensorDevicesConfig) ? this.sensorDevicesConfig[id] : null;
    }

    public getDeviceConfigurationByName(name: string): IDeviceConfiguration {
        for (const configKey in this.sensorDevicesConfig) {
            if (this.sensorDevicesConfig[configKey].getName() === name) {
                return this.sensorDevicesConfig[configKey];
            }
        }
        return null;
    }

    public getDeviceConfigurationByUUID(serviceUUID: string): IDeviceConfiguration {
        serviceUUID = serviceUUID.toLowerCase();
        for (const configKey in this.sensorDevicesConfig) {
            if (this.sensorDevicesConfig[configKey].getServiceUUID() === serviceUUID) {
                return this.sensorDevicesConfig[configKey];
            }
        }
        return null;
    }
}

export class BLESensorsConfiguration {
    private static readonly supportedSchema: string = "1";
    private static readonly sensorHubPrefix: string = "sensorHub_";
    private static readonly devicePrefix: string = "device_";
    private static readonly commandPrefix: string = "cmd_";
    private static readonly commandTypeMap: { [id: string]: CommandType } = {
        "read": CommandType.Read,
        "write": CommandType.Write
    };

    private configurationVersion: number;
    private sensorHubs: { [id: string]: SensorHubConfiguration; };
    constructor() {
        this.configurationVersion = 0;
        this.sensorHubs = {};
    }

    public reset() {
        this.configurationVersion = 0;
        for (const key in this.sensorHubs) {
            if (this.sensorHubs.hasOwnProperty(key)) {
                this.sensorHubs[key].reset();
                delete this.sensorHubs[key];
            }
        }
    }

    public async registerSensorsFromJSONFile(jsonDataFilePath: string): Promise<void> {
        console.info("Reading Config File: " + jsonDataFilePath);
        return new Promise<void>(async (resolve, reject) => {
            try {
                const contents = await fs.readTextFile(jsonDataFilePath);
                await this.registerSensorsFromJSON(contents);
                const parsedJSON = await JSON.parse(contents);
                this.parse(parsedJSON);
                resolve();
            } catch (err) {
                console.error("Failed to parse JSON File " + err);
                reject(err);
            }
        });
    }

    public async registerSensorsFromJSON(json: any): Promise<void> {
        return new Promise<void>(async (resolve, reject) => {
            try {
                this.configurationVersion = 0;
                this.parse(json);
                resolve();
            } catch (err) {
                console.error("Failed to configure JSON data " + err);
                reject(err);
            }
        });
    }

    public async print(): Promise<void> {
        return new Promise<void>((resolve) => {
            console.log("");
            for (const sid in this.sensorHubs) {
                if (this.sensorHubs.hasOwnProperty(sid)) {
                    const sensorHub: SensorHubConfiguration = this.sensorHubs[sid];
                    console.log(sensorHub.toString());
                    console.log(" " + sensorHub.getId()  + " Devices:");
                    const deviceConfigs = sensorHub.getDevicesConfiguration();
                    for (let devCfgIdx = 0; devCfgIdx < deviceConfigs.length; devCfgIdx++) {
                        console.log("  " + deviceConfigs[devCfgIdx].toString());
                        console.log("   " + deviceConfigs[devCfgIdx].getName() + " Commands:");
                        const commands = deviceConfigs[devCfgIdx].getCommands();
                        for (let cmdIdx = 0; cmdIdx < commands.length; cmdIdx++) {
                            console.log("    " + commands[cmdIdx].toString());
                        }
                    }
                }
            }
            resolve();
        });
    }

    public getConfigurationVersion(): number {
        return this.configurationVersion;
    }

    public getSensorHubIds(): string[] {
        return Object.keys(this.sensorHubs);
    }

    public getSensorHubConfigurationById(id: string): ISensorHubConfiguration {
        if (id && (id.length > 0)) {
            for (id in this.sensorHubs) {
                if (this.sensorHubs.hasOwnProperty(id)) {
                    return this.sensorHubs[id];
                }
            }
        }
        return null;
    }

    public getSensorHubConfigurationByName(name: string): ISensorHubConfiguration {
        if (name && (name.length > 0)) {
            for (const configKey in this.sensorHubs) {
                if (this.sensorHubs[configKey].getName() === name) {
                    return this.sensorHubs[configKey];
                }
            }
        }
        return null;
    }

    public getSensorHubConfigurationByUUID(uuid: string): ISensorHubConfiguration {
        if (uuid && (uuid.length > 0)) {
            for (const configKey in this.sensorHubs) {
                if (this.sensorHubs[configKey].getUUID() === uuid) {
                    return this.sensorHubs[configKey];
                }
            }
        }
        return null;
    }

    // todo move all parsing to another class and instantiate parser based on schema version
    private parseDeviceCommand(commandKey: string, parsedCommand: any): IDeviceCommand {
        const typeKey: string = parsedCommand.type;
        if (typeKey in BLESensorsConfiguration.commandTypeMap) {
            const commandType = BLESensorsConfiguration.commandTypeMap[typeKey];
            const deviceCommand = new DeviceCommand(commandKey, commandType,
                                                    parsedCommand.characteristicUUID.toLowerCase(),
                                                    parsedCommand.data || "",
                                                    parsedCommand.defaultIntervalInMs || 0);
            return deviceCommand;
        }
        return null;
    }

    private parseSensorDevices(deviceKey: string, parsedSensorHubDevice: any): DeviceConfiguration {
        let result = null;
        if (parsedSensorHubDevice && parsedSensorHubDevice.name && parsedSensorHubDevice.serviceUUID) {
            const deviceName = parsedSensorHubDevice.name;
            const serviceUUID = parsedSensorHubDevice.serviceUUID;
            const offset = BLESensorsConfiguration.commandPrefix.length;
            let deviceCommands = [];
            for (const key of Object.keys(parsedSensorHubDevice)) {
                if (key.startsWith(BLESensorsConfiguration.commandPrefix)) {
                    const commandKey = key.substring(offset);
                    const parsedCommand = parsedSensorHubDevice[key];
                    const deviceCommand = this.parseDeviceCommand(commandKey, parsedCommand);
                    if (deviceCommand) {
                        deviceCommands.push(deviceCommand);
                    }
                }
            }
            result = new DeviceConfiguration(deviceKey,
                                deviceName,
                                serviceUUID,
                                deviceCommands);
        }
        return result;
    }

    private parseSensorHub(sensorHubKey: string, parsedSensorHub: any): SensorHubConfiguration {
        let result = null;
        if (parsedSensorHub && parsedSensorHub.name) {
            const sensorHubName = parsedSensorHub.name;
            const sensorHubUUID = parsedSensorHub.uuid || "";
            const offset = BLESensorsConfiguration.devicePrefix.length;
            let deviceConfigurations = [];
            for (const key of Object.keys(parsedSensorHub)) {
                if (key.startsWith(BLESensorsConfiguration.devicePrefix)) {
                    const deviceKey = key.substring(offset);
                    const device = parsedSensorHub[key];
                    const deviceConfig = this.parseSensorDevices(deviceKey, device);
                    if (deviceConfig) {
                        deviceConfigurations.push(deviceConfig);
                    }
                }
            }
            result = new SensorHubConfiguration(sensorHubKey,
                                                sensorHubName,
                                                sensorHubUUID,
                                                deviceConfigurations);
        }
        return result;
    }

    private parse(parsedJSON: any) {
        if ((parsedJSON.schema === BLESensorsConfiguration.supportedSchema) &&
            (parsedJSON.version) && !isNaN(+parsedJSON.version)) {
            this.configurationVersion = +parsedJSON.version;
            const offset = BLESensorsConfiguration.sensorHubPrefix.length;
            for (const key of Object.keys(parsedJSON)) {
                if (key.startsWith(BLESensorsConfiguration.sensorHubPrefix)) {
                    const sensorHubKey = key.substring(offset);
                    const sensorHub = parsedJSON[key];
                    const sensorHubConfig = this.parseSensorHub(sensorHubKey, sensorHub);
                    if (sensorHubConfig) {
                        if (this.sensorHubs[sensorHubKey]) {
                            delete this.sensorHubs[sensorHubKey];
                        }
                        this.sensorHubs[sensorHubKey] = sensorHubConfig;
                    }
                }
            }
        }
    }
}
