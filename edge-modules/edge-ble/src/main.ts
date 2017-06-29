import { BLESensorsConfiguration } from "./sensorconfiguration";

async function main() {
    try {
        let cfg = new BLESensorsConfiguration();
        await cfg.registerSenorsFromJSONFile("./config/sensors.json");
        cfg.printConfiguration()
    } catch(err) {
        console.error("Error During Configuration Parse");
    }
}

main();
