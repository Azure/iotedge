import { EventEmitter } from "events";

export enum CommandType {
    Read = 0,
    Write
}

export class SensorDeviceCommandNames {
    public static readonly powerOff = "powerOff";
    public static readonly powerOn = "powerOn";
    public static readonly powerStatus = "powerStatus";
    public static readonly modelNumber = "modelNumber";
    public static readonly serialNumber = "serialNumber";
    public static readonly firmwareRevision = "firmwareRevision";
    public static readonly hardwareRevision = "hardwareRevision";
    public static readonly softwareRevision = "softwareRevision";
    public static readonly manufacturerName = "manufacturerName";
    public static readonly readData = "readData";
    public static readonly reportInterval = "reportInterval";
}

export class SensorHubCommandNames {
    public static readonly powerOff = "powerOff";
    public static readonly powerOn = "powerOn";
}

export interface IDeviceCommand {
    id: string;
    type: CommandType;
    uuid: string;
    writeData: string;
    defaultIntervalInMs: number;
    toString(): string;
}

export interface SensorDeviceMessage {
    body: any;
    properties: { [key: string]: string };
    sensorDeviceId: string;
    sensorHubId: string;
}

export abstract class IDeviceConfiguration {
    public abstract getId(): string;
    public abstract getName(): string;
    public abstract getServiceUUID(): string;
    public abstract toString(): void;
    public abstract getCommands(): IDeviceCommand[];
    public abstract getCommandById(id: string): IDeviceCommand;
    public abstract getCommandByUUID(characteristicUUID: string): IDeviceCommand;
    public abstract reset(): void;
}

export abstract class ISensorHubConfiguration {
    public abstract getId(): string;
    public abstract getName(): string;
    public abstract getUUID(): string;
    public abstract getDevicesConfiguration(): IDeviceConfiguration[];
    public abstract getDeviceConfigurationById(id: string): IDeviceConfiguration;
    public abstract getDeviceConfigurationByName(name: string): IDeviceConfiguration;
    public abstract getDeviceConfigurationByUUID(serviceUUID: string): IDeviceConfiguration;
    public abstract reset(): void;
}

export abstract class ISensorDevice extends EventEmitter {
    constructor() {
        super();
    }
    public abstract getId(): string;
    public abstract getName(): string;
    public abstract getUUID(): string;
    public abstract getReportInterval(): number;
    public abstract async setReportInterval(timeIntervalInMs: number): Promise<void>;
    public abstract async executeCommand(commandId: string, commandData?: string): Promise<Buffer>;
}

export abstract class ISensorHub {
    public abstract getId(): string;
    public abstract getName(): string;
    public abstract getUUID(): string;
    public abstract getSensorDevices(): ISensorDevice[];
    public abstract getSensorDeviceById(id: string): ISensorDevice;
    public abstract getSensorDeviceByName(deviceName: string): ISensorDevice;
    public abstract getSensorDeviceByUUID(serviceUUID: string): ISensorDevice;
    public abstract async executeCommand(commandId: string, commandData?: string): Promise<Buffer>;
}

export abstract class ISensorManager {
    public abstract async initialize(timeoutInMs?: number): Promise<void>;
    public abstract getConfigurationVersion(): number;
    public abstract async updateSensorConfigurationFromFile(jsonFile: string): Promise<void>;
    public abstract async updateSensorConfigurationFromJSON(json: any): Promise<void>;
    public abstract async printConfiguration(): Promise<void>;
    public abstract async connectToSensorHubByName(name: string, timeoutInMs?: number): Promise<ISensorHub>;
    public abstract async connectToSensorHubById(id: string, timeoutInMs?: number): Promise<ISensorHub>;
    public abstract async connectToSensorHubByUUID(uuid: string, timeoutInMs?: number): Promise<ISensorHub>;
    public abstract getConnectedSensorHubs(): ISensorHub[];
    public abstract getConfiguredSensorHubIds(): string[];
    public abstract getSensorHubById(id: string): ISensorHub;
    public abstract getSensorHubByName(name: string): ISensorHub;
    public abstract getSensorHubByUUID(uuid: string): ISensorHub;
    public abstract async disconnectFromSensorHub(sensor: ISensorHub): Promise<void>;
}
