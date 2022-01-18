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

## Step 1: Get added to allow-lists

The first step is to email us the relevant info so we can add you to the correct allow-lists to proceed with the preview. You should've already received an email asking you to reply with the information. If not, send an email to edgetransactablemodulespreview@service.microsoft.com in the following format:
- **Title**: "[Company name]: onboard to transactable Edge modules private preview"
- **Body**: please send us
    - **IoT hub name** - we recommend creating a new IoT hub dedicated for the preview.
    - **Seller ID** of the account you'll use - you can find your seller ID in [Partner Center -> Account Settings -> Legal Info -> Developer](https://partner.microsoft.com/en-us/dashboard/account/v3/organization/legalinfo#developer) It's a 8-digit numeric ID.

**IoT Edge**: please download the preview version of IoT Edge here <link TBD> and install it on a non-production device. Please set EXPERIMENTAL_FEATURE = TRUE

**Azure CLI**: download and install this private version of Azure CLI IoT extension: <link TBD>.

## Step 2: Preparing your IoT Edge modules

To prepare the IoT Edge module to be transactable, it must use a new EdgeHub API to get the transaction information. Keep in mind that while Microsoft handles the usage pipeline and the billing for the offer, each instance of a transactable module is responsible for enforcing the transaction information. Specifically, the module calls the EdgeHub API, which in turn calls IoT Hub to get the source of truth (“what the customer is paying for”), and the returned information is useful for the module to decide on what to do.

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
    "publisherId": "contoso" ,
    "offerId": "moduleoffer" ,
    "planId": "premium" ,
    "validationTime": "2022-04-23T13:22:32.123Z"
}
```

## Step 3: Publishing the modules in Partner Center

This section shows steps to create a new transactable Edge module offer. For the private preview, the offer *must be live* to be deployable to an Edge device. To hide the offer from the public, ensure that each *plan* you create is hidden.

1. If you haven't done so, email edgetransactablemodulespreview@service.microsoft.com your MPN ID. Wait for a positive confirmation before proceeding.
1. Visit [Partner Center - Commercial Marketplace Overview](https://partner.microsoft.com/en-us/dashboard/commercial-marketplace/overview)
1. Click **+ New offer** and select **IoT Edge module**
1. Give the new offer a descriptive name and alias, then click **Create**
1. In the refreshed page, click **Plan overview** and **+ Create new plan**
1. Give the plan a descriptive name and ID, then click **Create**
1. In **Pricing and availability**, adjust the **available markets**, set your **pricing** (we recommend using the smallest possible amount for private preview), ande **hide** the plan
    ![image](https://user-images.githubusercontent.com/2320572/150000980-ba938e01-cead-4cfe-ad95-234062b3d5f8.png)
1. Complete the offer configuration as you normally would, including updating descritpion, uploading a picture, setting EULA, and specifying a container image.
1. Click **Review and publish**
1. Under **Notes for certification**, include a message to indicate that "this offer is created for the transactable edmodule private preview". The certification team will contact us to approve the offer.
1. Now that the offer is configured, email edgetransactablemodulespreview@service.microsoft.com again with the offer ID and publisher ID to get it allow-listed.
1. Once certification and allow-listing is complete, click **Go live**. The offer must be live in order for module deployment to work.

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
