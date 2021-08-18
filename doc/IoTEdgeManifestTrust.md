## Start Guide for IoT Edge Deployment Trust
IoT Edge Deployment Trust is a feature which provides data integrity of the deployment manifest JSON which is used to deploy the module container images. 

To get started, follow the steps below

### 1. Notary Content Trust to protect module images
First the module container image's data integrity must be protected by using Notary Content Trust as shown in the first three steps in [this](https://github.com/Azure/iotedge/blob/master/doc/NotaryContentTrust.md) document.

Once the module images are signed and published in the private Azure Container Registry, the registry server name and its credentials are updated in the deployment JSON.

### 2. Sign the deployment Manifest
Once the deployment JSON is updated with the registry credential details, now the Manifest Signer Client tool is used to sign the deployment manifest JSON. Follow the first three steps in [this](https://github.com/Azure/iotedge/blob/master/samples/dotnet/ManifestSignerClient/Readme.md).

### 3. Configure the IoT edge Daemon in Edge device.
Configure the IoT Edge Daemon to enable Notary Content Trust as shown in step 4 of [this](https://github.com/Azure/iotedge/blob/master/doc/NotaryContentTrust.md) document.

Configure the IoT Edge Daemon to enable ManifestS Signing as shown in step 4 of [this](https://github.com/Azure/iotedge/blob/master/samples/dotnet/ManifestSignerClient/Readme.md) document.

### 4. Deploy the Deployment JSON
Once all the configurations are in place, deploy the signed manifest JSON using Azure CLI tools. Example [here](https://docs.microsoft.com/en-us/cli/azure/iot/edge/deployment?view=azure-cli-latest).