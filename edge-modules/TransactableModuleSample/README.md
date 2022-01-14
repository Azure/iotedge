# IoT Edge Transactable Modules - Private Preview

## Introduction

Thanks for joining us for the private preview for IoT Edge transactable modules, which lets partners like you monetize your IoT Edge modules without having to build your own billing services. In this private preview, you'll learn how to prepare your modules, try publishing them in partner center, and test deploying them onto IoT Edge devices.

Here's a list of features that are supported. We have tested each of the features, but we haven't tested all the possible combinations in every possible order. Please try them out! If you find any bugs, please send us an email.

- Deleting Edge device
- Edge device going offline
- Layered deployments
- Deleting IoT hub
- Multiple modules on one Edge device
- Removing module from Edge device
- Updating offer pricing
- Stop sell offer

### Not included in this private preview

Not everything is included in this private preview. The following features are coming later

- Custom dimensions (for example, $cost/tag/module/month)
- Partner center analytics
- Azure portal UX for module deployment
- There's no SDK support for the module API call needed yet
- IoT Hub resource moves are not supported
- Disabled devices are not supported
- Nested edge configuration is not supported
- The subscription where the IoT Hub can't be transferred to a different billing owner until you contact us

## Timeline

This private preview runs from **Jan to March 2022**.

## Step 1: Get onboarded to the preview

**IoT Hub**: please send your non-production IoT hub name to edgetransactablemodulespreview@service.microsoft.com and we will add it to the private preview allow list. This allows the billing usage to flow from the IoT Hub to Microsoft's commerce system.

**Partner Center**: in the same emailedgetransactablemodulespreview@service.microsoft.com, include your partner center account ID. It also needs to be allow-listed

**IoT Edge**: please download the preview version of IoT Edge here <link TBD> and install it on a non-production device. Please set EXPERIMENTAL_FEATURE = TRUE[[JL1]](https://microsoft.sharepoint.com/teams/Azure_IoT/IoTPlat/Shared%20Documents/Edge/Docs/Specs/IoT%20Edge%20-%20Transactable%20Modules%20Private%20Preview%20Doc.docx#_msocom_1) [[JL2]](https://microsoft.sharepoint.com/teams/Azure_IoT/IoTPlat/Shared%20Documents/Edge/Docs/Specs/IoT%20Edge%20-%20Transactable%20Modules%20Private%20Preview%20Doc.docx#_msocom_2) 

**Azure CLI**: download and install this private version of Azure CLI IoT extension: <link TBD>.

## Step 2: Preparing your IoT Edge modules

To prepare the IoT Edge module to be transactable, it must use a new EdgeHub API to get the transaction information. Keep in mind that while Microsoft handles the usage pipeline and the billing for the offer, each instance of a transactable module is responsible for enforcing the transaction information. For this private preview, you should implement business logic to ensure the API is called each time the module starts. [[JL3]](https://microsoft.sharepoint.com/teams/Azure_IoT/IoTPlat/Shared%20Documents/Edge/Docs/Specs/IoT%20Edge%20-%20Transactable%20Modules%20Private%20Preview%20Doc.docx#_msocom_3) [[AA4]](https://microsoft.sharepoint.com/teams/Azure_IoT/IoTPlat/Shared%20Documents/Edge/Docs/Specs/IoT%20Edge%20-%20Transactable%20Modules%20Private%20Preview%20Doc.docx#_msocom_4) Use the returned transaction information to adjust behavior (such as enabling/disabling features) for your module.

- Publish offer first (you can't do this right now)
- Deploy module
- Get the key from the portal
- Put the key in the twin
- Use code to generate token
- Use HttpClient to call the new API
This doesn't do any IP protection (watermarking, obfuscating, labelling).

```
GET /devices/{deviceId}/modules/{moduleId}/purchase
```

Returned result:
```
{
    // Latest status of the purchase, all other fields are only valid if this is "completed"
    "purchaseStatus": "completed",

    // Publisher ID of the transactable module
    "publisherId":  <string>,

    // Offer ID of the transactable module
    "offerId":  <string>,

    // Plan ID of the transactable module
    "planId":  <string>,

    // Timestamp for when EdgeHub was able to sync with IoT Hub
    "validationTime": <DateTime>

}
```

Auth/Authz: credentials for moduleId required (module can make the request to get purchase information only for itself). You can use the SDK to get the auth token.

## Step 3: Publishing the modules in Partner Center

First, use this special link to visit the partner center <link TBD>

Then, create a new IoT Edge module offer:

![image](https://user-images.githubusercontent.com/2320572/149595393-ea0daddd-4161-4593-93ad-258ee9c59e3c.png)

During the creation wizard, note that you can now set a price for your offer. We recommend setting it to something very low for purpose of testing. [[JL6]](https://microsoft.sharepoint.com/teams/Azure_IoT/IoTPlat/Shared%20Documents/Edge/Docs/Specs/IoT%20Edge%20-%20Transactable%20Modules%20Private%20Preview%20Doc.docx#_msocom_6) Lastly, click publish.

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
john@Azure:~$: az iot edge deployment create --hub-name myIoTHub --deployment-id contosoDeployment ---content ./contoso.deployment.json --target-condition "deviceId='myEdgeDevice'" --priority 10 --auth-type login[[JL7]](https://microsoft.sharepoint.com/teams/Azure_IoT/IoTPlat/Shared%20Documents/Edge/Docs/Specs/IoT%20Edge%20-%20Transactable%20Modules%20Private%20Preview%20Doc.docx#_msocom_7) [[JL8]](https://microsoft.sharepoint.com/teams/Azure_IoT/IoTPlat/Shared%20Documents/Edge/Docs/Specs/IoT%20Edge%20-%20Transactable%20Modules%20Private%20Preview%20Doc.docx#_msocom_8) [[JL9]](https://microsoft.sharepoint.com/teams/Azure_IoT/IoTPlat/Shared%20Documents/Edge/Docs/Specs/IoT%20Edge%20-%20Transactable%20Modules%20Private%20Preview%20Doc.docx#_msocom_9) 
```

The deployment manifest (contoso.deployment.json) requires a special section to indicate the module offer. Click [here]() to see a sample json file. The important section is to add a `modulesPurchase` at the end of the dpeloyment manifest, like this:

```
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
