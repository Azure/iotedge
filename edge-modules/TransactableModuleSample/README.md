# IoT Edge Transactable Modules - Private Preview

## Introduction

Thanks for joining us for the private preview for IoT Edge transactable modules, which lets partners like you monetize your IoT Edge modules without having to build your own billing services. In this private preview, you'll learn how to prepare your modules, try publishing them in partner center, and test deploying them onto IoT Edge devices.

Here's a list of features that are supported. We have tested each of the features, but we haven't tested all the possible combinations in every possible order. Please try them out! If you find any bugs, please send us an email at emailedgetransactablemodulespreview@service.microsoft.com.

## Features in this preview

| Feature                                                 | Supported?  | Expected behavior                            |
|---------------------------------------------------------|-------------|----------------------------------------------|
| Deleting Edge device                                    | ✅          | Billing stops                                |
| Edge device going offline                               | ✅          | Billing continues                            |
| Deleting IoT hub                                        | ✅          | Billing stops                                |
| Multiple modules on one Edge device                     | ✅          | Each module functions                        |
| Removing module from Edge device                        | ✅          | Billing stops                                |
| Updating offer pricing                                  | ✅          | Pricing is changed after waiting period      |
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

This private preview runs from **January to March 2022**.

## Step 1: Get onboarded to the preview

**IoT Hub**: please send your non-production IoT hub name to edgetransactablemodulespreview@service.microsoft.com and we will add it to the private preview allow list. This allows the billing usage to flow from the IoT Hub to Microsoft's commerce system.

**Partner Center**: in the same emailedgetransactablemodulespreview@service.microsoft.com, include your partner center account ID. It also needs to be allow-listed

**IoT Edge**: please download the preview version of IoT Edge here <link TBD> and install it on a non-production device. Please set EXPERIMENTAL_FEATURE = TRUE

**Azure CLI**: download and install this private version of Azure CLI IoT extension: <link TBD>.

## Step 2: Preparing your IoT Edge modules

To prepare the IoT Edge module to be transactable, it must use a new EdgeHub API to get the transaction information. Keep in mind that while Microsoft handles the usage pipeline and the billing for the offer, each instance of a transactable module is responsible for enforcing the transaction information. Specifically, the module calls the EdgeHub API, which in turn calls IoT Hub to get the source of truth (“what the customer is paying for”), and the returned information is useful for the module to decide on what to do.

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
    "publisherId": "contoso" ,
    "offerId": "moduleoffer" ,
    "planId": "premium" ,
    "validationTime": "2022-04-23T13:22:32.123Z"
}
```

## Step 3: Publishing the modules in Partner Center

First, use this special link to visit the partner center <link TBD>

Then, create a new IoT Edge module offer:

![image](https://user-images.githubusercontent.com/2320572/149595393-ea0daddd-4161-4593-93ad-258ee9c59e3c.png)

During the creation wizard, note that you can now set a price for your offer. We recommend setting it to something very low for purpose of testing. Lastly, click publish.

Create a plan with a very low cost and point it to your transatable module image.

Click Hide plan

Click publish and Go Live. In your description to the certification team, let them know that the offer is for private testing only.

## Step 4: Give user "IoT Hub Data Contributor" role

To deploy the module to an IoT Edge device, the user must use Azure AD authentication when interacting with IoT Hub. An IoT Hub Owner must grant the user (including themselves) the "IoT Hub Data Contributor" role:

![image](https://user-images.githubusercontent.com/2320572/149595576-0fbbcbae-33aa-48c2-829c-fdef7599815b.png)

Specifically, the user deploying the module must have the "Microsoft.Devices/IotHubs/configurations/write" data action. This can be assigned through other ways like custom roles, but giving the Data Contributor role is the easiest.

## Step 5: Deploying a transactable module to IoT Edge device

To deploy a transactable module to an IoT Edge device, use Azure CLI with the private IoT extension linked in step 1. These commands are similar and built upon the existing marketplace terms of use commands and Edge module deploy commands.

First, show the terms of use:

```
john@Azure:~$ az iot edge image terms show --publisher contoso --offer tempsensor --plan payg
```

Then, accept the terms of use:

```
john@Azure:~$ az iot edge image terms accept --publisher contoso --offer tempsensor --plan payg
```

Once the terms of use is accepted, deploy the module on an IoT Edge device. Here, the --auth-type login is important as deployment must be done with AAD authentication:

```
john@Azure:~$: az iot edge deployment create --hub-name myIoTHub --deployment-id contosoDeployment ---content ./contoso.deployment.json --target-condition "deviceId='myEdgeDevice'" --priority 10 --auth-type login
```

The deployment manifest (contoso.deployment.json) requires a special section to indicate the module offer. Click [here]() to see a sample json file. The important section is to add a `modulesPurchase` at the end of the dpeloyment manifest, like this:

```json
    "modulesPurchase": {
        "paid-module": {
            "publisherId": "azure-iot",
            "offerId": "jlian-test-offer-paid",
            "planId": "premium"
        }
    }
```

## Step 6: Validate everything works as you'd expect

Now that the module is deployed, wait an up to 36 hours for the usage to flow through the pipeline. You should see the module cost along with the offer details show up in Azure Cost Management:

![image](https://user-images.githubusercontent.com/2320572/149595711-86e1caed-6d82-4212-89a7-8d9ca03e7fb9.png)

Other places you can check includes the monthly invoice and TBD.

## Something doesn't work or doesn't make sense?

Please send any bug reports, feedback, and comments to emailedgetransactablemodulespreview@service.microsoft.com.
