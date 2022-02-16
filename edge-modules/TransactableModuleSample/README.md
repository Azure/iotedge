# IoT Edge Transactable Modules - Private Preview

## Introduction

Thanks for joining us for the private preview for IoT Edge transactable modules, which lets partners like you monetize your IoT Edge modules without having to build your own billing services. In this private preview, you'll learn how to prepare your modules, try publishing them in partner center, and test deploying them onto IoT Edge devices.

**Important**: As with any [private previews offered by Azure](https://azure.microsoft.com/support/legal/preview-supplemental-terms/), all features in this preview are subject to change and not available for production use. We'll do our best to keep things the same for later releases, but we cannot guarantee full compatibility. This means that the API could change, the offer specification could change, and that code and offers you create for this preview might not be supported in the future. If you feel comfortable, you could share this with a limited number of your own customers - but please share these caveats with them.

Here's a list of features that are supported. We have tested each of the support features, but we haven't tested all the possible combinations in every possible order. Please try them out! If you find any bugs beyond, please send us an email at emailedgetransactablemodulespreview@service.microsoft.com. Please don't attempt any of the unsupported scenarios like moving your IoT Hub resource. 

## Features in this preview

| Feature                                                 | Supported?  | Expected behavior                            |
|---------------------------------------------------------|-------------|----------------------------------------------|
| Deleting Edge device                                    | ✅          | Billing stops                                |
| Edge device going offline                               | ✅          | Billing continues                            |
| Deleting IoT hub                                        | ✅          | Billing stops                                |
| Multiple modules on one Edge device                     | ✅          | Each module functions                        |
| Removing module from Edge device                        | ✅          | Billing stops                                |
| Stop sell offer                                         | ✅          | Existing module works, new deployments don't |
| Layered deployments                                     | ❌          |                                              |
| Custom dimensions (for example, $cost/tag/module/month) | ❌          |                                              |
| Partner center analytics                                | ❌          |                                              |
| Azure portal UX for module deployment                   | ❌          |                                              |
| SDK support for the module API call needed yet          | ❌          |                                              |
| IoT Hub resource moves                                  | ❌          |                                              |
| Disabled devices                                        | ❌          |                                              |
| Nested edge configuration                               | ❌          |                                              |
| Subscription ownership transfer                         | ❌          |                                              |

## Timeline

This private preview runs from **February to May 2022**.

## Step 1: Prerequisites

**Allow-listing**: Email us the relevant info so we can add you to the correct allow-lists to proceed with the preview. You should've already received an email asking you to reply with the information. If not, send an email to edgetransactablemodulespreview@service.microsoft.com in the following format:
- **Title**: "[Company name]: onboard to transactable Edge modules private preview"
- **Body**: please send us
    - **IoT hub name** - we recommend creating a new IoT hub dedicated for the preview. For this preview, don't include any dashes ("-") in the IoT hub name.
    - **Offer ID** - please create (but not publish) an IoT Edge module offer that you'll use for this preview using [Partner Center](https://partner.microsoft.com/en-us/dashboard/commercial-marketplace/overview). Don't create any plans yet.

**IoT Edge**: please prepare a working IoT Edge device (VM or physical) that runs **Linux on AMD64** architecture. We recommend using an Ubuntu 18.04 VM.

Once you have the prerequisites - as in we've confirmed the allow-list over email, proceed to next steps.

## Step 2: Prepare your IoT Edge modules to enforce SKU

**Note**: to quickly get things going while full development is ongoing, start with our [sample code](https://github.com/Azure/iotedge/tree/feature/billing/edge-modules/TransactableModuleSample). To preview offer purchase and module deployment (without SKU enforcement described in this section), it might be easier to start with the container image that we built, linked from the [sample deployment manifest](./contoso.deployment.json). More info in Step 3 and onwards.

A transactable IoT Edge module should use a new EdgeHub API to get the offer and plan it's running with. Specifically, the module calls the EdgeHub API, which in turn calls IoT Hub to get the source of truth ("what the customer is paying for"), and the returned information is useful for the module to decide on what to do. Keep in mind that while Microsoft handles the usage pipeline and the billing for the offer, each instance of a transactable module is responsible for enforcing the SKU. For example, you might want to set a limit in the module to only support 100 tags if the customer is on a basic plan. In this case, the module should have logic to enforce that limit based on the offer and plan in the response of the API call.

![image](https://user-images.githubusercontent.com/2320572/149997134-77419272-c1e4-4856-a37e-be66c9652f97.png)

SDK support for this new API isn't available in the private preview, so the steps to integrate with this API involve more manual steps. In essence, you'll need to:

-   Deploy your unmodified module to an IoT Edge device
-   Get the SAS key for the module ([Azure portal example](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-csharp-csharp-module-twin-getstarted#update-the-module-twin-using-net-device-sdk))
-   Put the key in the module twin desired property through the deployment template ([example](https://docs.microsoft.com/en-us/azure/iot-edge/module-composition?view=iotedge-2020-11#define-or-update-desired-properties))
-   Update your module to read from the twin to get the key ([C# example](https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-csharp-module?view=iotedge-2020-11#update-the-module-with-custom-code))
-   Add code to generate the SAS token for the module ([example](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-dev-guide-sas?tabs=node#security-token-structure))
-   Create an HttpClient to call the new API in the module ([similar to this](https://github.com/Azure/iotedge/blob/master/edge-hub/core/src/Microsoft.Azure.Devices.Edge.Hub.CloudProxy/NestedDeviceScopeApiClient.cs#L163))
-   Create logic to react to the returned offer information, like enabling/disabling features in the module

Here is a link to a simple sample that demonstrates how above steps are done [iotedge/edge-modules/TransactableModuleSample at feature/billing - Azure/iotedge (github.com)](https://github.com/Azure/iotedge/tree/feature/billing/edge-modules/TransactableModuleSample).

In this sample, a large part of [PurchaseInfoProvider.cs](https://github.com/Azure/iotedge/blob/feature/billing/edge-modules/TransactableModuleSample/src/PurchaseInfoProvider.cs) is directly copied from [edge-util](https://github.com/Azure/iotedge/tree/master/edge-util/src/Microsoft.Azure.Devices.Edge.Util). This is a workaround to get the sample code to work while we finish the SDK integration. When the SDK integration is complete, you'll just need to make call to a built-in method GetPurchaseAsync, and it return the purchase status, publisher ID, offer ID, plan ID and validation time as specified.

## API specification 

```rest
GET /devices/{deviceId}/modules/{moduleId}/purchase
```

Returned result:

```json
{
	"purchaseStatus": "completed",
	"publisherId": "contoso",
	"offerId": "moduleoffer",
	"planId": "premium",
	"validationTime": "2022-04-23T13:22:32.123Z"
}
```

## Step 3: Publish the offer in Partner Center

This section shows steps to create a new transactable Edge module offer. For the private preview, the offer *must be live* to be deployable to an Edge device. To hide the offer from the public, ensure that each *plan* you create is hidden.

1. Visit [Partner Center - Commercial Marketplace Overview](https://partner.microsoft.com/en-us/dashboard/commercial-marketplace/overview)
1. Select the offer that you created earlier during for prerequisites. Only the allow-listed offer can have transactable plans.
1. Click **Plan overview** and **+ Create new plan**
1. Give the plan a descriptive name and ID, then click **Create**
1. In **Pricing and availability**, adjust the **available markets**, set your **pricing**. **To minimize any financial impact, please use the smallest possible amount, like $0.01/module/hour. Higher pricing without a justification will lead to delay or rejection during certification**. 
1. **Hide** the plan
    ![image](https://user-images.githubusercontent.com/2320572/150000980-ba938e01-cead-4cfe-ad95-234062b3d5f8.png)
1. Complete the offer configuration as you normally would, including updating description, uploading a picture, setting EULA, and specifying a container image (with the module you prepared in Step 2).
1. Click **Review and publish**
1. Under **Notes for certification**, include a message to indicate that "this offer is created for the transactable edge module private preview". The certification team will contact us to approve the offer.
1. Once certification and allow-listing is complete, click **Go live**. The offer must be live in order for module deployment to work.

## Step 4: Give user "IoT Hub Data Contributor" role

To deploy the module to an IoT Edge device, the user must use Azure AD authentication when interacting with IoT Hub. An IoT Hub Owner must grant the user (including themselves) the "IoT Hub Data Contributor" role

In Azure portal:

![image](https://user-images.githubusercontent.com/2320572/149595576-0fbbcbae-33aa-48c2-829c-fdef7599815b.png)

With Azure CLI:

```
az role assignment create --role 'IoT Hub Data Contributor' --assignee myuser@contoso.com --scope '/subscriptions/<your subscription>/resourceGroups/<your rg>/providers/Microsoft.Devices/IotHubs/<your iot hub name>'
```

Specifically, the user deploying the module must have the "Microsoft.Devices/IotHubs/configurations/write" data action. This can be assigned through other ways like custom roles, but giving the Data Contributor role is the easiest.

## Step 5: Deploying a transactable module to IoT Edge device

These commands are similar and built upon the existing marketplace terms of use commands and Edge module deploy commands.

1. If you haven't already, download and install Azure CLI
1. Install a special version of the Azure IoT CLI extension for this preview. It'll remove any older existing IoT CLI extension first:
    ```
    az extension remove --name azure-iot
    az extension add --source https://privatepreviewbilledge.blob.core.windows.net/packages/azure_iot-255.253.2-py3-none-any.whl
    ```
1. Use `az account set` to navigate to the subscription you plan to deploy the module
1. Show the terms of use for your offer. Substitute with your offer's publisher ID, offer ID, and plan ID:

    ```
    az iot edge image terms show --publisher contoso --offer tempsensor --plan payg
    ```
1. Then, accept the terms of use:

    ```
    az iot edge image terms accept --publisher contoso --offer tempsensor --plan payg
    ```
1. Once the terms of use are accepted, deploy the module on an IoT Edge device. Here, the `--auth-type login` is important as deployment must be done with AAD authentication:

    ```
    az iot edge deployment create --hub-name myIoTHub --deployment-id contosoDeployment ---content ./contoso.deployment.json --target-condition "deviceId='myEdgeDevice'" --priority 10 --auth-type login
    ```
1. And here's where we should talk about the deployment manifest.

### The deployment manifest 

The deployment manifest `contoso.deployment.json` requires a special section to indicate the module offer. Click [here](./contoso.deployment.json) to see a sample json file. The important section is to add a `modulesPurchase` at the end of the deployment manifest, like this:

```json
    "modulesPurchase": {
        "paid-module": {
            "publisherId": "contoso",
            "offerId": "tempsensor",
            "planId": "payg"
        }
    }
```

Inside the same sample deployment file, you might notice that the EdgeHub container image points to `iotedgebilling.azurecr.io/microsoft/azureiotedge-hub:20220210.2-linux-amd64` instead of the usual location. This special EdgeHub image has the API to send offer/plan info to a module that asks for it (from Step 2). Without it, the API wouldn't work.

Lastly, the manifest deploys a module from `iotedgebilling.azurecr.io/microsoft/transactable-module-0210`. This module's only job is to get the offer and plan info, then print it to console ([source code](https://github.com/Azure/iotedge/blob/feature/billing/edge-modules/TransactableModuleSample/src/Program.cs)). We prepared this sample transactable module in case you haven't prepared your own yet. Feel free to use it to kickstart your testing while development for your own module is ongoing.

## Step 6: Validate everything works as you'd expect

Now that the module is deployed, wait an up to 36 hours for the usage to flow through the pipeline. You should see the module cost along with the offer details show up in Azure Cost Management:

![image](https://user-images.githubusercontent.com/2320572/149595711-86e1caed-6d82-4212-89a7-8d9ca03e7fb9.png)

Other places you can check includes the monthly invoice, which should show something like this:

![image](https://user-images.githubusercontent.com/2320572/153516259-fe5d4bcb-4443-4dd5-909e-db556828fc39.png)

## Something doesn't work or doesn't make sense?

**Please send any bug reports, feedback, and comments to edgetransactablemodulespreview@service.microsoft.com.**

### Known issues

- IoT Hub name cannot contain any dashes `-`.
- In Cost Management, usage records show up weirdly with weird resource IDs (instead of a link to the IoT Hub) and service name "IoT Service" instead of something more appropriate like "IoT Edge Module".
- There's no easy way to directly query the IoT Hub to see which offers are deployed to what module.
