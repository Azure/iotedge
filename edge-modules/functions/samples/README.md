# Azure functions module for IoT Edge
This is a sample on how to use EdgeHub binding for Azure functions in an IoT Edge module. 
It contains a C# function that gets triggered when a message is received from EdgeHub and also contains docker files needed to build the module image.

Current version is based on Azure functions runtime 3.0 which has a larger docker image size compared to previous version. If image size is a concern older version of EdgeHub binding (<=1.0.7) has to be used which is based on Azure functions 2.0