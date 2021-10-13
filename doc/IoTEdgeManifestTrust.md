## Start Guide for IoT Edge Deployment Trust
IoT Edge Deployment Trust is a feature which provides data integrity of the deployment manifest JSON which is used to deploy the module container images. 

To get started, follow the steps of the quick start guide of deploying modules to an IoT edge device as shown in this [link](https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux?view=iotedge-2020-11). Follow the step where a review of the deployment of the module is done as shown in [this](https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux?view=iotedge-2020-11#review-and-create) step. Do not click **Review + create** but note down the image URL of all the modules that needs to be deployed to the device. Now get another Linux device which is different from the IoT edge device where the images are signed and published by following the steps below.

### 1. Notary Content Trust to protect module images
Get the container images downloaded into the machine which are configured in the deployment JSON by using  `docker pull`. First the module container image's data integrity must be protected by using Notary Content Trust as shown in the first three steps in [this](https://github.com/Azure/iotedge/blob/master/doc/NotaryContentTrust.md) document.

Once the module images are signed and published in the private Azure Container Registry, the registry server name and its credentials are updated in the deployment JSON as shown in the **Set modules** step in this [link](https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux?view=iotedge-2020-11#modules). Again copy the Deployment JSON with the updated credentials of the Azure Container Registry which is used in the next step.

### 2. Sign the deployment Manifest
Once the deployment JSON is updated with the registry credential details, now the Manifest Signer Client tool is used to sign the deployment manifest JSON. Follow the first three steps in [this](https://github.com/Azure/iotedge/blob/master/samples/dotnet/ManifestSignerClient/Readme.md). After this we have a signed deployment JSON ready to be deployed. 

### 3. Configure the IoT edge Daemon in Edge device.
Configure the IoT Edge Daemon to enable Notary Content Trust as shown in step 4 of [this](https://github.com/Azure/iotedge/blob/master/doc/NotaryContentTrust.md) document.

Configure the IoT Edge Daemon to enable ManifestS Signing as shown in step 4 of [this](https://github.com/Azure/iotedge/blob/master/samples/dotnet/ManifestSignerClient/Readme.md) document.

### 4. Deploy the Deployment JSON
Once all the configurations are in place, deploy the signed deployment JSON from step 2 using Azure CLI tools. Example [here](https://docs.microsoft.com/en-us/cli/azure/iot/edge/deployment?view=azure-cli-latest). Now the modules status can be viewed by following the reminder of the quick start guide from this [link](https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux?view=iotedge-2020-11#view-generated-data)
