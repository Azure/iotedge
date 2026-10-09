# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for
# full license information.
# Migrated with IoTHub Python SDK v2

import asyncio
import time
import sys
import json
from threading import Lock
import logging
# from azure.core.credentials import AzureKeyCredential
from azure.iot.device.aio import IoTHubModuleClient
from azure.iot.device import Message, MethodResponse
from datetime import datetime

logging.basicConfig(level=logging.DEBUG)

mutex = Lock()
# global counters
TIME_INTERVAL = 10 # in sec
CONFIG_TELE = "configmsg"


class HubManager(object):
    def __init__(self):
        self.module_client = IoTHubModuleClient.create_from_edge_environment()

    async def start(self):
        await self.module_client.connect()
        # set the received data handlers on the client
        self.module_client.on_message_received = self.message_handler
        self.module_client.on_method_request_received = self.method_handler

    async def preprocess(self, message):
        # print("in preprocess function!")
        message_str = message.data
        if not message_str:
            return None
        message_obj = json.loads(message_str)
        print("module receives a msg with temp: {}".format(message_obj["machine"]["temperature"]))
        input_data = message_obj["machine"]["temperature"]
        time_stamp = message_obj["timeCreated"]  # UTC iso format. "1972-01-01T00:00:00Z" is UTC ISO 8601

        return [time_stamp, input_data]

    async def config_telemetry(self, message, **kwargs): 
        # print("in config_telemetry")

        for key, value in kwargs.items():
            if key == "time_interval":# default time_interval value = 10
                print("config_telemetry time_interval ", value)
                print("message ts ", message[0], type(message[0]))
                
                for iter_sec in range(0, 60, value):
                    print("iter_sec", iter_sec)
                    print('message[0][17:19].strip()', message[0][17:19].strip(), type(message[0][17:19].strip()))
                    if int(message[0][17:19].strip()) == iter_sec:
                        message_str = json.dumps(message) 
                        result = Message(message_str)
                        print("telemetry to be send to iothub: ", message_str)
                        await self.forward_event_to_output(result, "output2")


    async def message_handler(self, message):
        print("in message_handler")
        print ("message_handler TIME_INTERVAL", TIME_INTERVAL)      
        if message.input_name == "input1":
            mutex.acquire()
            try:
                sensor_input = await self.preprocess(message) 
                if sensor_input!= None:
                    print("config freq and sending...")
                    await self.config_telemetry(sensor_input, time_interval= TIME_INTERVAL)
            except Exception as e:
                print("Error when config telemetry: %s" % e)
            finally: 
                mutex.release()

        else:
            print("message received on unknown input")


    # Direct Method receiver
    async def method_handler(self, method_request):
        print("Received method [%s]" % (method_request.name))
        print("config_tele_messsage: ",method_request.payload)
        global TIME_INTERVAL
        TIME_INTERVAL = method_request.payload
        print ("method_handler TIME_INTERVAL", TIME_INTERVAL)
        print("Sent method response to module output via event [%s]" % CONFIG_TELE)
        method_response = MethodResponse.create_from_method_request(
            method_request, 200, "{ \"Response\": \"This is the response from the device. \" }"
        )
        await self.module_client.send_method_response(method_response)

    async def forward_event_to_output(self, event, moduleOutputName):
        await self.module_client.send_message_to_output(event, moduleOutputName)

async def main():
    try:
        print("\nPython %s\n" % sys.version)
        print("Prototype for config IoT Edge module")

        hub_manager = HubManager()
        await hub_manager.start()
        print("The sample is now waiting for messages and will indefinitely.  Press Ctrl-C to exit. ")

        while True:
            time.sleep(1)

    except KeyboardInterrupt:
        await hub_manager.module_client.shutdown()
        print("Configuration sample stopped")


if __name__ == '__main__':
    asyncio.run(main())
