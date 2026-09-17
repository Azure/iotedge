
# Introduction

This Direct Method Sample is an amendment based on Azure IoT C SDK filter module example where the direct method handler is missing.

The customer code-with engagement may leverage this sample by registering moduleMethodCallback function with moduleClient object and initializing in SetupCallbacksForModule.

1. Add moduleMethodCallback function in main.c
2. Initialise IoTHubModuleClient_LL_SetModuleMethodCallback under SetupCallbacksForModule function in main.c

The sample is tested on IoT Edge VM Ubuntu18.04, via azure CLI deployment with manifest located at ./module/CModule/deploymentManual.json
