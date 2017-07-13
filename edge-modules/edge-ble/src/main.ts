import * as fs from "async-file";
import { BLEEdgeModule } from "./bleedgemodule";
import { ISensorManager } from "./blemodule";
import { SensorManager } from "./sensormanager";

interface ModuleConfig {
    moduleConfigFilePath: string;
    moduleConnectionString: string;
    sensorManager: ISensorManager;
}

class Main {
    private static readonly exitMessages: { [msg: string]: string } = {
        "SIGHUP": " Hang Up Requested. Exiting...",
        "SIGINT": " Ctrl-C Pressed. Exiting...",
        "SIGTERM": " SIGTERM Received. Force Exiting...",
        "exit": " Exiting...",
        "uncaughtException": " Uncaught Exception Observed. Exiting..."
    };
    private static readonly sensorsConfigFile = "./config/sensors.json";
    private static conf: ModuleConfig;
    private static bleModule: BLEEdgeModule = null;
    private static currentExitState = "notStarted";

    public static async run() {
        if (Main.currentExitState === "notStarted") {
            Main.currentExitState = "started";
            try {
                process.on("SIGHUP", () => Main.applicationExitHandler("SIGHUP"));
                process.on("SIGINT", () => Main.applicationExitHandler("SIGINT"));
                process.on("SIGTERM", () => Main.applicationExitHandler("SIGTERM"));
                process.on("exit", () => Main.applicationExitHandler("exit", process.exitCode));
                process.on("uncaughtException", () => Main.applicationExitHandler("uncaughtException", 1));
                await Main.prepareModuleConfig();
                Main.bleModule = new BLEEdgeModule(Main.conf.moduleConnectionString,
                                                Main.conf.sensorManager, Main.conf.moduleConfigFilePath);
                await Main.bleModule.start();
            } catch (err) {
                console.error("Error During BLE Module Initialization " + err);
                process.exit(1);
            }
        }
    }

    private static applicationExitHandler = (reason: string, exitCode?: number)  => {
        const exitCodeString = (typeof(exitCode) !== "undefined") ? ` Exit code: ${exitCode}` : "";
        const exitCodeVal = (typeof(exitCode) !== "undefined") ? exitCode : 0;
        console.log(Main.exitMessages[reason] + exitCodeString);
        if (Main.currentExitState === "started") {
            Main.currentExitState = "exiting";
            if (Main.bleModule) {
                Main.bleModule.stop();
            }
            if (reason !== "exit") {
                process.exit(exitCodeVal);
            }
        }
    }

    private static async prepareModuleConfig(): Promise<void> {
        return new Promise<void>(async (resolve, reject) => {
            const moduleConnString: string = process.env.EdgeHubConnectionString;
            if (!moduleConnString) {
                reject("Env Variable EdgeHubConnectionString Not Set");
            } else {
                const filePath = process.env.SensorsConfigFile || Main.sensorsConfigFile;
                const fileExists = await fs.exists(filePath);
                if (!fileExists) {
                    reject("Sensor Hubs Config File Not Found");
                } else {
                    Main.conf = {
                                    moduleConfigFilePath: filePath,
                                    moduleConnectionString: moduleConnString,
                                    sensorManager: new SensorManager()
                                };
                    resolve();
                }
            }
        });
    }
}

Main.run();
