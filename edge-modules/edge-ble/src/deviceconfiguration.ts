export class CommandTypes {
    static readonly powerOff = "powerOff";
    static readonly powerOn = "powerOn";
    static readonly modelNumber = "modelNumber";
    static readonly serialNumber = "serialNumber";
    static readonly firmwareRevision = "firmwareRevision";
    static readonly hardwareRevision = "hardwareRevision";
    static readonly softwareRevision = "softwareRevision";
    static readonly manufacturerName = "manufacturerName";
}

export class DeviceCommand {
    readonly id: string;
    readonly uuid: string;
    readonly writeData: string;

    constructor(id:string, uuid:string, writeData?:string) {
        this.id = id;
        this.uuid = uuid;
        this.writeData = writeData || "";
    }

    toString() {
        return `{${this.id}, ${this.uuid}, ${this.writeData}}`;
    }
}

export class DeviceConfiguration {
    readonly id: string;
    readonly name: string;
    readonly commands: { [id: string] : DeviceCommand; };

    constructor(id:string, name:string, deviceCommands: DeviceCommand[]) {
        this.id = id;
        this.name = name;
        this.commands = {};
        for (let command of deviceCommands) {
            this.commands[command.id] = command;
        }
    }

    toString() {
        return `{${this.id}, ${this.name}}`;
    }
}
