import * as fs from 'async-file';
import {
    DeviceConfiguration,
    DeviceCommand
} from "./deviceconfiguration";

export class SensorConfiguration extends DeviceConfiguration {
    readonly sensorDevices: { [id: string] : DeviceConfiguration; };

    constructor(id:string, name:string, sensorCommands: DeviceCommand[], devices:DeviceConfiguration[]) {
        super(id, name, sensorCommands);
        this.sensorDevices = {};
        for (let device of devices) {
            this.sensorDevices[device.id] = device;
        }
    }
}

export class BLESensorsConfiguration {
    private sensors: { [id: string] : SensorConfiguration; };

    constructor() {
        this.sensors = {};
    }

    public async registerSenorsFromJSONFile(jsonDataFilePath: string): Promise<any> {
        console.info("Reading Config File: " + jsonDataFilePath);
        return new Promise(async (resolve, reject) => {
            try {
                let contents = await fs.readTextFile(jsonDataFilePath);
                await this.registerSenorsFromJSON(contents);
                resolve();
            } catch (err) {
                reject("Invalid BLE Sensors JSON Config File");
            }
        });
    }

    public async registerSenorsFromJSON(jsonData: string): Promise<any> {
        return new Promise(async (resolve, reject) => {
            try {
                let parsedJSON = await JSON.parse(jsonData);
                this.parseSensorData(parsedJSON);
                resolve();
            } catch (err) {
                reject("Invalid Sensor JSON Data");
            }
        });
    }

    public printConfiguration() {
        console.log("");
        for (let sid in this.sensors) {
            let sensor: SensorConfiguration = this.sensors[sid];
            console.log(sensor.toString());

            console.log(" " + sensor.id  + " Commands:");
            for (let cid in sensor.commands) {
                let command: DeviceCommand = sensor.commands[cid];
                console.log("  " + command.toString());
            }

            console.log(" " + sensor.id  + " Devices:");
            for (let did in sensor.sensorDevices) {
                let device: DeviceConfiguration = sensor.sensorDevices[did];
                console.log("  " + device.toString());
                console.log("   " + device.id  + " Commands:");
                for (let cid in device.commands) {
                    let command: DeviceCommand = device.commands[cid];
                    console.log("    " + command.toString());
                }
            }
        }
    }

    private parseSensorData(parsedJSON:any) {
        for (const sensorKey of Object.keys(parsedJSON.sensors)) {
            let devices = [];
            let sensor = parsedJSON.sensors[sensorKey];
            for (const deviceKey of Object.keys(sensor.devices)) {
                let deviceCommands = [];
                let device = sensor.devices[deviceKey];
                for (const commandKey of Object.keys(device.commands)) {
                    let parsedCommand = device.commands[commandKey];
                    let command = new DeviceCommand(commandKey, parsedCommand.characteristic_uuid, parsedCommand.data);
                    deviceCommands.push(command);
                }
                let deviceConfig = new DeviceConfiguration(deviceKey, device.name, deviceCommands);
                devices.push(deviceConfig);
            }
            let sensorCommands = [];
            for (const commandKey of Object.keys(sensor.commands)) {
                let parsedCommand = sensor.commands[commandKey];
                let command = new DeviceCommand(commandKey, parsedCommand.characteristic_uuid, parsedCommand.data);
                sensorCommands.push(command);
            }
            let sensorConfig = new SensorConfiguration(sensorKey, sensor.name, sensorCommands, devices);
            this.sensors[sensorKey] = sensorConfig;
        }
    }
}
