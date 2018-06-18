"use strict";

const Protocol = require("azure-iot-device-mqtt").Mqtt;
const { ModuleClient, Message } = require("azure-iot-device");

const config = {
  messageInterval: process.env.MessageInterval || 2000,
  maxTemp: process.env.MaxTemp || 40
};

function main() {
  ModuleClient.fromEnvironment(Protocol, (err, client) => {
    if (err) {
      console.error(`Error creating ModuleClient instance: ${err}`);
    } else {
      client.open(err => {
        if (err) {
          console.error(`Connection error: ${err}`);
        } else {
          // register event handler for incoming messages
          client.on("inputMessage", (inputName, msg) => {
            client.complete(msg, printResultFor("completed"));
            const data = Buffer.from(msg.data).toString();
            console.log(`<- Data: ${JSON.stringify(JSON.parse(data))}`);
          });

          client.onDeviceMethod("reset", (req, res) => {
            console.log(
              `Got method call [${req.requestId}, ${
                req.methodName
              }, ${JSON.stringify(req.payload, null, 2)}]`
            );
            res.send(
              200,
              "Device reset successful",
              printResultFor("method response")
            );
          });

          // send an event out every few seconds
          const messageSend = () => {
            const data = {
              temperature: Math.floor(Math.random() * config.maxTemp)
            };
            client.sendOutputEvent(
              "nodeTemperatureOutput",
              new Message(JSON.stringify(data)),
              err => {
                if (err) {
                  console.error(`Message send error: ${err}`);
                } else {
                  console.log(`-> Data: ${data.temperature}`);
                  setTimeout(messageSend, config.messageInterval);
                }
              }
            );
          };
          setTimeout(messageSend, config.messageInterval);
        }
      });
    }
  });
}

function printResultFor(op) {
  return function printResult(err, res) {
    if (err) console.log(op + " error: " + err.toString());
    if (res) console.log(op + " status: " + res.constructor.name);
  };
}

main();
