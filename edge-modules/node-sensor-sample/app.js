'use strict';

const Protocol = require('azure-iot-device-mqtt').Mqtt;
const { Client: DeviceClient, Message } = require('azure-iot-device');

const config = {
  connectionString: process.env.EdgeHubConnectionString,
  messageInterval: process.env.MessageInterval || 2000,
  maxTemp: process.env.MaxTemp || 40
};

function main() {
  const client = DeviceClient.fromConnectionString(config.connectionString, Protocol);
  client.open(err => {
    if (err) {
      console.error(`Connection error: ${err}`);
    } else {
      // register event handler for incoming messages
      client.on('event', msg => {
        console.log(JSON.stringify(msg, null, 2));
      });

      // send an event out every few seconds
      const messageSend = () => {
        const data = {
          temperature: Math.floor(Math.random() * config.maxTemp)
        };
        client.sendEvent(
          'nodeTemperatureOutput',
          new Message(JSON.stringify(data)),
        err => {
          if (err) {
            console.error(`Message send error: ${err}`);
          } else {
            setTimeout(messageSend, config.messageInterval);
          }
        });
      };
      setTimeout(messageSend, config.messageInterval);
    }
  });
}

main();
