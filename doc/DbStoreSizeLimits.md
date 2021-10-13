# How to configure database store size limits of Edge Hub

By default, the IoT Edge system module Edge Hub stores data in an on device disk-based database store. This database store is used to store configuration data and messages.

Users can configure the EdgeHub to use an in-memory store instead, by setting the `UsePersistentStorage` environment variable to `false`.

**It must be noted that this feature to configure database store size limits is currently only supported for the in-memory database store of Edge Hub i.e., when the environment variable `UsePersistentStorage` is set to `false`. The work to support this feature for the disk-based database store is currently in progress.**

## __Use case__

Customers who wish to use the in-memory database store of Edge Hub and also wish to control the maximum size that the database can grow to, will find this feature useful.

## __Configuration__

To enable a max size limit of the in-memory database stores, please set the `MaxSizeBytes` property in the `StoreLimits` configuration section, of the `StoreAndForwardConfiguration` desired property element of Edge Hub, to the maximum size in bytes that you wish to limit the database store size to.

When a maximum size limit for the database store is specified, Edge Hub will ensure that the message store size doesn't exceeded the specified limit before attempting to add a new item. If the message store size does exceed the specified limit, Edge Hub will stop accepting new messages from other modules and throw a `StorageFullException`.
Only when the message store decreases in size will Edge Hub start accepting new messages. The size of the message store can reduce due to successful message delivery to IoT hub or on TTL expiry.

## __Example__

### __How to set a store size limit__

Here's an example of how to set the store size limit for Edge Hub through Az CLI:

Create a deployment manifest `deployment.json` JSON file that has your IoT Edge deployment specification. Please refer to [Learn how to deploy modules and establish routes in IoT Edge][1] for more information about the IoT Edge deployment manifest (including a sample manifest that describes the `edgeHub` configuration section of the manifest).

The feature is specifically controlled using the following section of the `edgeHub` configuration in the deployment manifest:

```JSON
"storeAndForwardConfiguration": {
    "timeToLiveSecs": 7200,
    "storeLimits": {
        "maxSizeBytes": 1000000
    }
}
```

Please refer to [Deploy Azure IoT Edge modules with Azure CLI][2] for steps on how to deploy the deployment.json file to your device.

Once the deployment completes, the Edge Hub module running on your device should now start honoring the store size limit of 1000000 bytes as per your deployment manifest.

### __How to remove the size limit__

The store size limit can be cleared by modifying the deployment manifest to remove the `storeLimits` section entirely and issuing another IoT Edge deployment to your device using the new manifest.

Here's what the `storeAndForwardconfiguration` section of the deployment manifest should look like to clear the `storeLimits` specified earlier:

```JSON
"storeAndForwardConfiguration": {
    "timeToLiveSecs": 7200
}
```

[1]: https://docs.microsoft.com/azure/iot-edge/module-composition
[2]: https://docs.microsoft.com/azure/iot-edge/how-to-deploy-modules-cli