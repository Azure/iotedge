// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdio.h>
#include <stdlib.h>

#include "iothub_module_client_ll.h"
#include "iothub_client_options.h"
#include "iothub_message.h"
#include "azure_c_shared_utility/threadapi.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_c_shared_utility/platform.h"
#include "azure_c_shared_utility/shared_util_options.h"
#include "iothubtransportmqtt.h"
#include "iothub.h"
#include "time.h"
#include "parson.h"

static double temperatureThreshold = 25;

typedef struct MESSAGE_INSTANCE_TAG
{
    IOTHUB_MESSAGE_HANDLE messageHandle;
    size_t messageTrackingId;  // For tracking the messages within the user callback.
} 
MESSAGE_INSTANCE;

size_t messagesReceivedByInput1Queue = 0;

// SendConfirmationCallback is invoked when the message that was forwarded on from 'InputQueue1Callback'
// pipeline function is confirmed.
static void SendConfirmationCallback(IOTHUB_CLIENT_CONFIRMATION_RESULT result, void* userContextCallback)
{
    // The context corresponds to which message# we were at when we sent.
    MESSAGE_INSTANCE* messageInstance = (MESSAGE_INSTANCE*)userContextCallback;
    printf("Confirmation[%zu] received for message with result = %d\r\n", messageInstance->messageTrackingId, result);
    IoTHubMessage_Destroy(messageInstance->messageHandle);
    free(messageInstance);
}

// Allocates a context for callback and clones the message
// NOTE: The message MUST be cloned at this stage.  InputQueue1Callback's caller always frees the message
// so we need to pass down a new copy.
static MESSAGE_INSTANCE* CreateMessageInstance(IOTHUB_MESSAGE_HANDLE message)
{
    MESSAGE_INSTANCE* messageInstance = (MESSAGE_INSTANCE*)malloc(sizeof(MESSAGE_INSTANCE));
    
    if ((messageInstance->messageHandle = IoTHubMessage_Clone(message)) == NULL)
    {
        free(messageInstance);
        messageInstance = NULL;
    }
    else
    {
        messageInstance->messageTrackingId = messagesReceivedByInput1Queue;

        //add a new property to the message, which labels the message as an alert
        MAP_HANDLE propMap = IoTHubMessage_Properties(messageInstance->messageHandle);
        if (Map_AddOrUpdate(propMap, "MessageType", "Alert") != MAP_OK)
        {
           printf("ERROR: Map_AddOrUpdate Failed!\r\n");
        }
    }

    return messageInstance;
}

//This function implements the actual messaging filter
//When a message is received, it checks whether the reported temperature 
//exceeds the threshold. If yes, then it forwards the message through its 
//output queue. If not, then it ignores the message.

static unsigned char *bytearray_to_str(const unsigned char *buffer, size_t len)
{
    unsigned char *ret = (unsigned char *)malloc(len + 1);
    memcpy(ret, buffer, len);
    ret[len] = '\0';
    return ret;
}

static void moduleTwinCallback(DEVICE_TWIN_UPDATE_STATE update_state, const unsigned char* payLoad, size_t size, void* userContextCallback)
{
    printf("\r\nTwin callback called with (state=%s, size=%zu):\r\n%s\r\n",
        MU_ENUM_TO_STRING(DEVICE_TWIN_UPDATE_STATE, update_state), size, payLoad);
    JSON_Value *root_value = json_parse_string(payLoad);
    JSON_Object *root_object = json_value_get_object(root_value);
    if (json_object_dotget_value(root_object, "desired.TemperatureThreshold") != NULL) {
        temperatureThreshold = json_object_dotget_number(root_object, "desired.TemperatureThreshold");
    }
    if (json_object_get_value(root_object, "TemperatureThreshold") != NULL) {
        temperatureThreshold = json_object_get_number(root_object, "TemperatureThreshold");
    }
}

static void moduleMethodCallback(const char* method_name, const unsigned char* payload, size_t size, unsigned char** response, size_t* resp_size, void* userContextCallback)
{

    const int METHOD_RESPONSE_SUCCESS = 200;
    const int METHOD_RESPONSE_ERROR = 401;
    int status = 501;
    const char* RESPONSE_STRING = "{ \"Response\": \"Unknown method requested.\" }";
    
    printf("\r\nMethod callback called with (method_name=%s):\r\n%s\r\n", method_name, payload);
    RESPONSE_STRING = "{ \"Response\": \"This is a response from cc's iotedgeVM1.\" }";
    status = METHOD_RESPONSE_SUCCESS;
    printf("\r\nResponse status: %d\r\n", status);
    printf("Response payload: %s\r\n\r\n", RESPONSE_STRING);

    *resp_size = strlen(RESPONSE_STRING);

    if ((*response = (unsigned char*)malloc(*resp_size)) == NULL)
    {
        printf("memory allocation for response failure");
        status = METHOD_RESPONSE_ERROR;
    }
    else
    {
        memcpy(*response, RESPONSE_STRING, *resp_size);
        printf("OK - sending response to cloud");
        status = METHOD_RESPONSE_SUCCESS;
    }

}

static IOTHUBMESSAGE_DISPOSITION_RESULT InputQueue1Callback(IOTHUB_MESSAGE_HANDLE message, void* userContextCallback)
{
    IOTHUBMESSAGE_DISPOSITION_RESULT result;
    IOTHUB_CLIENT_RESULT clientResult;
    IOTHUB_MODULE_CLIENT_LL_HANDLE iotHubModuleClientHandle = (IOTHUB_MODULE_CLIENT_LL_HANDLE)userContextCallback;

    unsigned const char* messageBody;
    size_t contentSize;

    if (IoTHubMessage_GetByteArray(message, &messageBody, &contentSize) == IOTHUB_MESSAGE_OK)
    {
        messageBody = bytearray_to_str(messageBody, contentSize);
    } 
    else
    {
        messageBody = "<null>";
    }

    printf("Received Message [%zu]\r\n Data: [%s]\r\n",
            messagesReceivedByInput1Queue, messageBody);

    // Check if the message reports temperatures higher than the threshold
    JSON_Value *root_value = json_parse_string(messageBody);
    JSON_Object *root_object = json_value_get_object(root_value);
    double temperature;
    if (json_object_dotget_value(root_object, "machine.temperature") != NULL && (temperature = json_object_dotget_number(root_object, "machine.temperature")) > temperatureThreshold)
    {
        printf("Machine temperature %f exceeds threshold %f\r\n", temperature, temperatureThreshold);
        // This message should be sent to next stop in the pipeline, namely "output1".  What happens at "outpu1" is determined
        // by the configuration of the Edge routing table setup.
        MESSAGE_INSTANCE *messageInstance = CreateMessageInstance(message);
        if (NULL == messageInstance)
        {
            result = IOTHUBMESSAGE_ABANDONED;
        }
        else
        {
            printf("Sending message (%zu) to the next stage in pipeline\n", messagesReceivedByInput1Queue);

            clientResult = IoTHubModuleClient_LL_SendEventToOutputAsync(iotHubModuleClientHandle, messageInstance->messageHandle, "output1", SendConfirmationCallback, (void *)messageInstance);
            if (clientResult != IOTHUB_CLIENT_OK)
            {
                IoTHubMessage_Destroy(messageInstance->messageHandle);
                free(messageInstance);
                printf("IoTHubModuleClient_LL_SendEventToOutputAsync failed on sending msg#=%zu, err=%d\n", messagesReceivedByInput1Queue, clientResult);
                result = IOTHUBMESSAGE_ABANDONED;
            }
            else
            {
                result = IOTHUBMESSAGE_ACCEPTED;
            }
        }
    }
    else
    {
        printf("Not sending message (%zu) to the next stage in pipeline.\r\n", messagesReceivedByInput1Queue);
        result = IOTHUBMESSAGE_ACCEPTED;
    }

    messagesReceivedByInput1Queue++;
    return result;
}

static IOTHUB_MODULE_CLIENT_LL_HANDLE InitializeConnection()
{
    IOTHUB_MODULE_CLIENT_LL_HANDLE iotHubModuleClientHandle;

    if (IoTHub_Init() != 0)
    {
        printf("Failed to initialize the platform.\r\n");
        iotHubModuleClientHandle = NULL;
    }
    else if ((iotHubModuleClientHandle = IoTHubModuleClient_LL_CreateFromEnvironment(MQTT_Protocol)) == NULL)
    {
        printf("ERROR: IoTHubModuleClient_LL_CreateFromEnvironment failed\r\n");
    }
    else
    {
        // Uncomment the following lines to enable verbose logging.
        // bool traceOn = true;
        // IoTHubModuleClient_LL_SetOption(iotHubModuleClientHandle, OPTION_LOG_TRACE, &trace);
    }

    return iotHubModuleClientHandle;
}

static void DeInitializeConnection(IOTHUB_MODULE_CLIENT_LL_HANDLE iotHubModuleClientHandle)
{
    if (iotHubModuleClientHandle != NULL)
    {
        IoTHubModuleClient_LL_Destroy(iotHubModuleClientHandle);
    }
    IoTHub_Deinit();
}

static int SetupCallbacksForModule(IOTHUB_MODULE_CLIENT_LL_HANDLE iotHubModuleClientHandle)
{
    int ret;

    if (IoTHubModuleClient_LL_SetInputMessageCallback(iotHubModuleClientHandle, "input1", InputQueue1Callback, (void*)iotHubModuleClientHandle) != IOTHUB_CLIENT_OK)
    {
        printf("ERROR: IoTHubModuleClient_LL_SetInputMessageCallback(\"input1\")..........FAILED!\r\n");
        ret = MU_FAILURE;
    }
    else if (IoTHubModuleClient_LL_SetModuleTwinCallback(iotHubModuleClientHandle, moduleTwinCallback, (void*)iotHubModuleClientHandle) != IOTHUB_CLIENT_OK)
    {
        printf("ERROR: IoTHubModuleClient_LL_SetModuleTwinCallback(default)..........FAILED!\r\n");
        ret = MU_FAILURE;
    }
    else if (IoTHubModuleClient_LL_SetModuleMethodCallback(iotHubModuleClientHandle, moduleMethodCallback, (void*)iotHubModuleClientHandle) != IOTHUB_CLIENT_OK)
    {
        printf("ERROR: IoTHubModuleClient_LL_SetModuleMethodCallback(default)..........FAILED!\r\n");
        ret = MU_FAILURE;
    }
    else
    {
        ret = 0;
    }

    return ret;
}

void iothub_module()
{
    IOTHUB_MODULE_CLIENT_LL_HANDLE iotHubModuleClientHandle;

    srand((unsigned int)time(NULL));

    if ((iotHubModuleClientHandle = InitializeConnection()) != NULL && SetupCallbacksForModule(iotHubModuleClientHandle) == 0)
    {
        // The receiver just loops constantly waiting for messages.
        printf("Waiting for incoming messages.\r\n");
        while (true)
        {
            IoTHubModuleClient_LL_DoWork(iotHubModuleClientHandle);
            ThreadAPI_Sleep(100);
        }
    }

    DeInitializeConnection(iotHubModuleClientHandle);
}

int main(void)
{
    iothub_module();
    return 0;
}
