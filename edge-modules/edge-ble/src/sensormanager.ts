import { EventEmitter } from "events";
import * as noble from "noble";
import { BLESensorsConfiguration } from "./sensorconfiguration";
import {
    CommandType,
    IDeviceConfiguration,
    ISensorDevice,
    ISensorHub,
    ISensorHubConfiguration,
    ISensorManager,
    SensorDeviceCommandNames,
    SensorDeviceMessage
} from "./blemodule";


noble.on("stateChange", (state: any) => {
    console.info("State Change Event: " + state);
    NobleHelper.setBLEAvailable(state === "poweredOn");
});

class TimedOperationHelper extends EventEmitter {
    private timeout: NodeJS.Timer;

    constructor(timeoutInMs: number) {
        super();
        this.timeout = setTimeout(() => {
            this.emit("timeout");
        }, timeoutInMs);
    }

    public complete(args: any) {
        clearTimeout(this.timeout);
        this.emit("complete", args);
    }

    public reject(args: any) {
        clearTimeout(this.timeout);
        this.emit("reject", args);
    }
}

class NobleHelper {
    private static availabilityEmitter = new EventEmitter();
    private static isAvailable = false;

    public static setBLEAvailable(permitted: boolean) {
        NobleHelper.isAvailable = permitted;
        NobleHelper.availabilityEmitter.emit("bleAvailabilityStateChanged", permitted);
    }

    public static isBLEAvailable(): boolean {
        return NobleHelper.isAvailable;
    }

    public static async isBLEAvailableAsync(timeoutInMs: number): Promise<boolean> {
        if (NobleHelper.isBLEAvailable()) {
            return Promise.resolve(true);
        } else {
            let timeoutId: NodeJS.Timer = null;

            const timeoutPromise = new Promise<boolean>((resolve) => {
                timeoutId = setTimeout(() => {
                    console.debug("Timeout for BLE Availability");
                    resolve(false);
                }, timeoutInMs);
            });

            const availabilityPromise = new Promise<boolean>((resolve) => {
                NobleHelper.availabilityEmitter.once("bleAvailabilityStateChanged", (available: boolean) => {
                    if (timeoutId) {
                        clearTimeout(timeoutId);
                    }
                    if (available) {
                        resolve(true);
                    } else {
                        console.debug("BLE Unavailable");
                        resolve(false);
                    }
                });
            });

            return Promise.race<boolean>([timeoutPromise, availabilityPromise]);
        }
    }

    public static startScanning(): void {
        noble.startScanning();
    }

    public static stopScanning(): void {
        noble.stopScanning();
    }

    public static peripheralConnect(peripheral: noble.Peripheral): Promise<void> {
        return new Promise<void>((resolve, reject) => {
            peripheral.connect((err: any) => {
                if (err) {
                    reject(err);
                } else {
                    resolve();
                }
            });
        });
    }

    public static peripheralDisconnect(peripheral: noble.Peripheral): Promise<void> {
        return new Promise<void>((resolve) => {
            peripheral.disconnect(() => {
                resolve();
            });
        });
    }

    public static peripheralDiscoverServices(peripheral: noble.Peripheral,
                                                serviceUUIDs?: string[]): Promise<noble.Service[]> {
        return new Promise((resolve, reject) => {
            peripheral.discoverServices(serviceUUIDs, (err, services) => {
                if (err) {
                    reject(err);
                } else {
                    resolve(services);
                }
            });
        });
    }

    public static serviceDiscoverCharacteristics(service: noble.Service,
                                            characteristicUUIDs?: string[]): Promise<noble.Characteristic[]> {
        return new Promise((resolve, reject) => {
            service.discoverCharacteristics(characteristicUUIDs, (err, characteristics) => {
                if (err) {
                    reject(err);
                } else {
                    resolve(characteristics);
                }
            });
        });
    }

    public static executeCommand(characteristic: noble.Characteristic,
                            type: CommandType,
                            writeData?: string): Promise<Buffer> {
        return new Promise((resolve, reject) => {
            if (type === CommandType.Write) {
                const command = Buffer.from(writeData, "hex");
                characteristic.write(command, false, (err) => {
                    if (err) {
                        console.error("Write Command Failed" + err);
                        reject(new Error("Write Execute Error " + err));
                    } else {
                        resolve(null);
                    }
                });
            } else {
                characteristic.read((err, data) => {
                    if (err) {
                        console.error("Read Command Failed" + err);
                        reject(new Error("Read Execute Error " + err));
                    } else {
                        resolve(data);
                    }
                });
            }
        });
    }
}

class Scanner {
    private static readonly retryTimeoutInMs: number = 5000;
    private refCount: number;

    constructor() {
        this.refCount = 0;
        noble.on("scanStart", () => console.info("Scan Started Event"));
        noble.on("scanStop", () => console.info("Scan Stopped Event"));
    }

    public isScanningPermitted() {
        return NobleHelper.isBLEAvailable();
    }

    public scanStart() {
        let count = this.refCount++;
        if (count === 0) {
            this.reScan();
        }
    }

    public async scanStop() {
        if (this.refCount !== 0) {
            if (--this.refCount === 0) {
                 await NobleHelper.stopScanning();
            }
        }
    }

    private async reScan() {
        if (this.refCount !== 0) {
            try {
                await NobleHelper.stopScanning();
                await NobleHelper.startScanning();
                setTimeout(() => {
                    if (this.refCount) {
                        this.reScan();
                    }
                }, Scanner.retryTimeoutInMs);
            } catch (err) {
                console.error("Error Observed During Scanning");
            }
        }
    }
}

class SensorDevice extends ISensorDevice {
    private sensorHubId: string;
    private deviceConfiguration: IDeviceConfiguration;
    private reportInterval: number;
    private reportIntervalTimer: NodeJS.Timer;
    private service: noble.Service;
    private characteristics: { [id: string]: noble.Characteristic; };
    private powerState: number;

    constructor(sensorHubId: string,
                service: noble.Service,
                characteristics: noble.Characteristic[],
                deviceConfiguration: IDeviceConfiguration) {
        super();
        this.sensorHubId = sensorHubId;
        this.service = service;
        this.characteristics = {};
        for (let idx = 0; idx < characteristics.length; idx++) {
            this.characteristics[characteristics[idx].uuid] = characteristics[idx];
        }
        this.deviceConfiguration = deviceConfiguration;
        const cmd = this.deviceConfiguration.getCommandById(SensorDeviceCommandNames.reportInterval);
        if (cmd) {
            this.reportInterval = Math.max(0, cmd.defaultIntervalInMs);
        } else {
            this.reportInterval = 0;
        }
        this.reportIntervalTimer = null;
        this.powerState = 0;
    }

    public getId(): string {
        return this.deviceConfiguration.getId();
    }

    public getName(): string {
        return this.deviceConfiguration.getName();
    }

    public getUUID(): string {
        return this.service.uuid;
    }

    public getReportInterval(): number {
        return this.reportInterval;
    }

    public async setReportInterval(timeIntervalInMs: number): Promise<void> {
        console.debug("setReportInterval " + timeIntervalInMs);
        return new Promise<void>((resolve, reject) => {
            if (timeIntervalInMs >= 0) {
                if (this.reportIntervalTimer) {
                    clearTimeout(this.reportIntervalTimer);
                    this.reportIntervalTimer = null;
                }
                this.reportInterval = timeIntervalInMs;
                if (this.reportInterval > 0) {
                    this.readReportIntervalData();
                }
                resolve();
            } else {
                reject(new Error("Invalid Time Interval Argument"));
            }
        });
    }

    public async executeCommand(commandId: string, commandData?: string): Promise<Buffer> {
        console.debug("Execute Sensor Device Command:" + commandId);
        return new Promise<Buffer>(async (resolve, reject) => {
            let characteristic: noble.Characteristic;
            const cmd = this.deviceConfiguration.getCommandById(commandId);
            if (cmd && this.characteristics[cmd.uuid]) {
                console.debug("Found Command In Configuration:" + cmd +
                                " UUID:" + cmd.uuid + " Type: " + cmd.type);
                characteristic = this.characteristics[cmd.uuid];
                try {
                    const writeData = commandData || cmd.writeData;
                    const data = await NobleHelper.executeCommand(characteristic, cmd.type, writeData);
                    console.debug("Command Executed Successfully");
                    this.updateState(commandId, writeData);
                    resolve(data);
                } catch (err) {
                    console.error("Error In Command Execute:" + commandId + " " + err);
                    reject(new Error("Error In Command Execute"));
                }
            } else {
                console.error("Cannot Find Characteristic For Command: " + commandId);
                reject(new Error("Cannot Find Characteristic"));
            }
        });
    }

    private updateState(commandId: string, writeData?: string) {
        if (commandId === SensorDeviceCommandNames.powerOff) {
            this.powerState = 0;
        } else if (commandId === SensorDeviceCommandNames.powerOn) {
            this.powerState = 1;
            this.readReportIntervalData();
        } else if (commandId === SensorDeviceCommandNames.reportInterval) {
            if (writeData) {
                const data = Number(writeData);
                if (!isNaN(data)) {
                    console.log("Setting Period: " + parseInt(writeData, 10));
                    this.setReportInterval(parseInt(writeData, 10));
                }
            }
        }
    }

    private async readReportIntervalData() {
        if ((this.powerState === 1) && (this.reportInterval > 0)) {
            const data: Buffer = await this.executeCommand(SensorDeviceCommandNames.reportInterval);
            const dataMsg = data.toString("hex");
            const deviceId = this.getId();
            const msg: SensorDeviceMessage = {
                body: { sample: `${dataMsg}`,
                        source: `${this.sensorHubId}/${deviceId}` },
                properties: null,
                sensorDeviceId: deviceId,
                sensorHubId: this.sensorHubId
            };
            this.emit("reportIntervalData", msg);
            this.reportIntervalTimer = setTimeout(() => {
                this.readReportIntervalData();
            }, this.reportInterval);
        }
    }
}

class Sensor extends ISensorHub {
    private peripheral: noble.Peripheral;
    private readonly sensorHubConfig: ISensorHubConfiguration;
    private services: { [id: string]: SensorDevice; };
    private name: string;
    private id: string;
    private uuid: string;

    constructor(peripheral: noble.Peripheral, sensorHubConfig: ISensorHubConfiguration) {
        super();
        this.sensorHubConfig = sensorHubConfig;
        this.peripheral = peripheral;
        this.services = {};
    }

    public getName() {
        return this.sensorHubConfig.getName();
    }

    public getId() {
        return this.sensorHubConfig.getId();
    }

    public getUUID() {
        return this.peripheral.uuid;
    }

    public async connect(): Promise<void> {
        return new Promise<void>(async (resolve, reject) => {
            try {
                await NobleHelper.peripheralConnect(this.peripheral);
                console.info("Connected to peripheral: " + this.peripheral.uuid);
                const services = await NobleHelper.peripheralDiscoverServices(this.peripheral);
                for (let svcIdx = 0; svcIdx < services.length; svcIdx++) {
                    const service = services[svcIdx];
                    console.info("Service: " + service.uuid);
                    const characteristics = await NobleHelper.serviceDiscoverCharacteristics(service);
                    for (let charIdx = 0; charIdx < characteristics.length; charIdx++) {
                        console.info(">>>Characteristic: " + characteristics[charIdx].uuid);
                    }
                    const deviceConfiguration = this.sensorHubConfig.getDeviceConfigurationByUUID(service.uuid);
                    if (deviceConfiguration) {
                        console.info("Adding Service: " + service.uuid);
                        const sensorDevice = new SensorDevice(this.getId(),
                                                                service,
                                                                characteristics,
                                                                deviceConfiguration);
                        this.services[service.uuid] = sensorDevice;
                    }
                }
                resolve();
            } catch (err) {
                console.error("Error Observed Connecting to peripheral: " + this.peripheral.uuid + " " + err);
                reject(err);
            }
        });
    }

    public disconnect = async (): Promise<void> => {
        return new Promise<void>(async (resolve) => {
            try {
                 await this.powerCtlDevices(SensorDeviceCommandNames.powerOff);
            } catch (err) {
                 console.info("Could Not Power Off All Sensor Devices During Disconnect");
            } finally {
                try {
                    await NobleHelper.peripheralDisconnect(this.peripheral);
                } catch (err) {
                    console.error("Cannot Disconnect From Sensor Hub " + this.id);
                }
            }
            let services = this.services;
            Object.keys(services).forEach(function(key) { delete services[key]; });
            resolve();
        });
    }

    public getSensorDevices(): ISensorDevice[] {
        let result: ISensorDevice[];
        result = [];
        for (const serviceUUID of Object.keys(this.services)) {
            const device = this.services[serviceUUID];
            if (this.sensorHubConfig.getDeviceConfigurationByUUID(serviceUUID)) {
                result.push(device);
            }
        }

        return result;
    }

    public getSensorDeviceById(id: string): ISensorDevice {
        let result = null;

        const deviceConfig = this.sensorHubConfig.getDeviceConfigurationById(id);
        if (deviceConfig) {
            result = this.getSensorDeviceByUUID(deviceConfig.getServiceUUID());
        } else {
            console.error("Error Did Not Find Sensor Device For Id " + id);
        }

        return result;
    }

    public getSensorDeviceByName(deviceName: string): ISensorDevice {
        let result = null;

        const deviceConfig = this.sensorHubConfig.getDeviceConfigurationByName(deviceName);
        if (deviceConfig) {
            result = this.getSensorDeviceByUUID(deviceConfig.getServiceUUID());
        } else {
            console.error("Error Did Not Find Sensor Device For Name " + deviceName);
        }

        return result;
    }

    public getSensorDeviceByUUID(uuid: string): ISensorDevice {
        let result = null;

        if (uuid in this.services) {
            result = this.services[uuid];
        } else {
            console.error("Error Did Not Find Sensor Device For UUID " + uuid);
        }

        return result;
    }

    public async executeCommand(commandId: string, commandData?: string): Promise<Buffer> {
        console.debug("Execute Sensor Hub Command:" + commandId);
        if ((commandId === SensorDeviceCommandNames.powerOff) ||
            (commandId === SensorDeviceCommandNames.powerOn)) {
            return this.powerCtlDevices(commandId);
        } else {
            return Promise.reject(new Error("Invalid Sensor Hub Command " + commandId));
        }
    }

    private async powerCtlDevices(command: string): Promise<Buffer> {
        return new Promise<Buffer>(async (resolve) => {
            for (const serviceUUID of Object.keys(this.services)) {
                const device = this.services[serviceUUID];
                try {
                    await device.executeCommand(command);
                } catch (err) {
                    console.error("Error During Sensor Device " + device.getId() + " " + command);
                }
            }
            resolve(null);
        });
    }
}

export class SensorManager extends ISensorManager {
    private static readonly scanByUUID  = "uuid";
    private static readonly scanByName  = "name";
    private static readonly scanTimeoutMs = 15000;
    private static readonly RSSI_THRESHOLD = -90;
    private inReset: boolean;
    private connectedSensors: { [id: string]: Sensor; };
    private aggregateScanRequests: { [type: string]: { [id: string]: TimedOperationHelper; }; };
    private scanner: Scanner;
    private readonly sensorsConfig: BLESensorsConfiguration;

    constructor() {
        super();
        this.inReset = false;
        this.connectedSensors = {};
        this.aggregateScanRequests = {};
        this.aggregateScanRequests[SensorManager.scanByUUID] = {};
        this.aggregateScanRequests[SensorManager.scanByName] = {};
        this.scanner = new Scanner();
        this.sensorsConfig = new BLESensorsConfiguration();
        noble.on("discover", this.onDiscover);
        noble.on("disconnect", this.onDisconnect);
    }

    public getConfigurationVersion(): number {
        return this.sensorsConfig.getConfigurationVersion();
    }

    public async updateSensorConfigurationFromFile(jsonFile: string): Promise<void> {
        return new Promise<void>(async (resolve, reject) => {
            try {
                await this.resetState();
                await this.sensorsConfig.registerSensorsFromJSONFile(jsonFile);
                resolve();
            } catch (err) {
                console.error("Could Not Update Sensor Config From JSON File " + jsonFile);
                reject(err);
            }
        });
    }

    public async updateSensorConfigurationFromJSON(json: any): Promise<void> {
        return new Promise<void>(async (resolve, reject) => {
            try {
                await this.resetState();
                await this.sensorsConfig.registerSensorsFromJSON(json);
                resolve();
            } catch (err) {
                console.error("Could Not Update Sensor Config From JSON");
                reject(err);
            }
        });
    }

    public async printConfiguration(): Promise<void> {
        return await this.sensorsConfig.print();
    }

    public getConnectedSensorHubs(): ISensorHub[] {
        let connectedSensors = this.connectedSensors;
        return Object.keys(connectedSensors).map((key) => connectedSensors[key]);
    }

    public getConfiguredSensorHubIds(): string[] {
        return this.sensorsConfig.getSensorHubIds();
    }

    public getSensorHubById(id: string): ISensorHub {
        if (id && (id.length > 0)) {
            for (let key in this.connectedSensors) {
                if (this.connectedSensors[key].getId() === id) {
                    return this.connectedSensors[key];
                }
            }
        }
        return null;
    }

    public getSensorHubByName(name: string): ISensorHub {
        if (name && (name.length > 0)) {
            for (let key in this.connectedSensors) {
                if (this.connectedSensors[key].getName() === name) {
                    return this.connectedSensors[key];
                }
            }
        }
        return null;
    }

    public getSensorHubByUUID(uuid: string): ISensorHub {
        if (uuid && uuid.length > 0) {
            return (uuid in this.connectedSensors) ? this.connectedSensors[uuid] : null;
        }
        return null;
    }

    public async initialize(timeoutInMs: number = SensorManager.scanTimeoutMs): Promise<void> {
        return new Promise<void>(async (resolve, reject) => {
            try {
                const isAvailable = await NobleHelper.isBLEAvailableAsync(timeoutInMs);
                if (isAvailable) {
                    resolve();
                } else {
                    reject();
                }
            } catch (err) {
                console.error("Could Not Initialize BLE");
                reject(err);
            }
        });
    }

    public connectToSensorHubById(id: string, timeoutInMs: number = SensorManager.scanTimeoutMs): Promise<ISensorHub> {
        console.debug("connectToSensorById: " + id);
        let sensorHub = this.getSensorHubById(id);
        if (sensorHub) {
            return Promise.resolve(sensorHub);
        }
        let sensorHubConfig = this.sensorsConfig.getSensorHubConfigurationById(id);
        if (sensorHubConfig) {
            return this.connect(sensorHubConfig.getName(), SensorManager.scanByName, timeoutInMs);
        }
        return Promise.reject(new Error("Sensor Hub With ID " + id + " Not Found"));
    }

    public connectToSensorHubByName(name: string, timeoutInMs: number = SensorManager.scanTimeoutMs): Promise<ISensorHub> {
        console.debug("connectToSensorByName: " + name);
        let sensorHub = this.getSensorHubByName(name);
        if (sensorHub) {
            return Promise.resolve(sensorHub);
        }
        return this.connect(name, SensorManager.scanByName, timeoutInMs);
    }

    public connectToSensorHubByUUID(uuid: string, timeoutInMs: number = SensorManager.scanTimeoutMs): Promise<ISensorHub> {
        console.debug("connectToSensorHubByUUID: " + uuid);
        let sensorHub = this.getSensorHubByUUID(uuid);
        if (sensorHub) {
            return Promise.resolve(sensorHub);
        }
        return this.connect(uuid, SensorManager.scanByUUID, timeoutInMs);
    }

    public disconnectFromSensorHub(sensorHub: ISensorHub): Promise<void> {
        const sensor = sensorHub as Sensor;
        if (sensor == null) {
            Promise.reject(new Error("NULL Sensor"));
        } else {
            const uuid = sensor.getUUID();
            if (uuid in this.connectedSensors) {
                console.info("Disconnecting Sensor: " + uuid);
                delete this.connectedSensors[uuid];
                return sensor.disconnect();
            } else {
                Promise.reject(new Error("Unknown Sensor"));
            }
        }
    }

    private connect(key: string, type: string, timeoutInMs: number): Promise<ISensorHub> {
        if ((!key) || (key.length === 0)) {
            Promise.reject(new Error("Invalid Argument"));
        } else if (this.inReset) {
            Promise.reject(new Error("In Reset State"));
        } else if (!this.scanner.isScanningPermitted()) {
            Promise.reject(new Error("Cannot Scan For BLE Devices"));
        } else if (key in this.aggregateScanRequests[type]) {
            Promise.reject(new Error("Scanning In Progress For Request " + key + " By " + type));
        } else {
            return this.performConnect(key, type, timeoutInMs);
        }
    }

    private onDisconnect = () => {
        console.debug("Sensor Hub Disconnect Complete");
    }

    private resetState = async (): Promise<void> => {
        return new Promise<void>(async (resolve) => {
            console.info("Resetting Sensor Manager");
            this.inReset = true;

            // reject any pending connect requests
            for (const type in this.aggregateScanRequests) {
                if (this.aggregateScanRequests.hasOwnProperty(type)) {
                    for (const key in this.aggregateScanRequests[type]) {
                        if (this.aggregateScanRequests[type].hasOwnProperty(key)) {
                            this.aggregateScanRequests[type][key].reject("Resetting Hub");
                        }
                    }
                }
            }

            // disconnect all connected sensor hubs
            for (const sensorHubId of Object.keys(this.connectedSensors)) {
                const sensorHub = this.connectedSensors[sensorHubId];
                try {
                    await sensorHub.disconnect();
                } catch (err) {
                    console.error("Could Not Disconnect Sensor Hub " + sensorHubId);
                }
            }

            let connectedSensors = this.connectedSensors;
            Object.keys(connectedSensors).forEach(function(key) { delete connectedSensors[key]; });
            this.sensorsConfig.reset();
            this.inReset = false;
            console.info("Resetting Sensor Manager Complete");
            resolve();
        });
    }

    private onDiscover = (peripheral: noble.Peripheral): void  => {
        if (!this.inReset) {
            console.debug("Peripheral discovered " +
                    "(UUID (MAC): " + peripheral.id +
                    ", Is Connectable: " + peripheral.connectable +
                    ", RSSI: " + peripheral.rssi +
                    ", Local Name: " + peripheral.advertisement.localName + ")");

            if (!(peripheral.connectable) || (peripheral.rssi < SensorManager.RSSI_THRESHOLD)) {
                return;
            }

            const localName = peripheral.advertisement.localName;
            const uuid = peripheral.id;
            let sensorConfig: ISensorHubConfiguration;
            let timedOperation = null;
            if (localName in this.aggregateScanRequests[SensorManager.scanByName]) {
                sensorConfig = this.sensorsConfig.getSensorHubConfigurationByName(localName);
                timedOperation = this.aggregateScanRequests[SensorManager.scanByName][localName];
                delete this.aggregateScanRequests[SensorManager.scanByName][localName];
            } else if (uuid in this.aggregateScanRequests[SensorManager.scanByUUID]) {
                sensorConfig = this.sensorsConfig.getSensorHubConfigurationByUUID(uuid);
                timedOperation = this.aggregateScanRequests[SensorManager.scanByUUID][uuid];
                delete this.aggregateScanRequests[SensorManager.scanByUUID][uuid];
            }

            if (sensorConfig) {
                console.info("Peripheral Connected " +
                    "(UUID (MAC): " + peripheral.id +
                    ", Local Name: " + peripheral.advertisement.localName + ")");
                const sensor = new Sensor(peripheral, sensorConfig);
                this.connectedSensors[uuid] = sensor;
                timedOperation.complete(uuid);
            }
        }
    }

    private async performConnect(key: string, type: string, timeoutInMs: number): Promise<ISensorHub> {
        return new Promise<ISensorHub>(async (resolve, reject) => {
            const timedOperation = new TimedOperationHelper(timeoutInMs);
            this.aggregateScanRequests[type][key] = timedOperation;
            this.scanner.scanStart();

            timedOperation.on("complete", async (uuid: string) => {
                console.debug("Connect Complete By " + type + " For: " + key);
                this.scanner.scanStop();
                let sensor = this.connectedSensors[uuid];
                try {
                    await sensor.connect();
                    resolve(sensor);
                } catch (err) {
                    console.error("Sensor Connect Failed: " + name);
                    reject(err);
                }
            });

            timedOperation.on("timeout", async () => {
                this.scanner.scanStop();
                delete this.aggregateScanRequests[type][key];
                console.error("Timeout During Scan For Sensor");
                reject(new Error("Timeout During Scan"));
            });

            timedOperation.on("reject", async () => {
                this.scanner.scanStop();
                delete this.aggregateScanRequests[type][key];
                console.error("Rejected Request");
                reject(new Error("Rejected Request"));
            });
        });
    }
}

