# How to configure database store size limits of Edge Hub

By default, the IoT Edge system module Edge Hub stores data in an on device disk-based database store. This database store is used to store configuration data and messages.

Support for an in-memory database store is also provided for customers who wish to limit the amount of disk I/O on the device and can be enabled by setting the `UsePersistentStorage` environment variable of the module to `false`.

The maximum size of the in-memory database store can be controlled by configuring the limit in the desired properties of the module.

**It must be noted that this feature is currently only supported for the in-memory database store of Edge Hub i.e., when the environment variable `UsePersistentStorage` is set to `false`. The work to support this feature for the disk-based database store is currently in progress.**

## __Use case__

Customers who wish to use the in-memory database store of Edge Hub and also wish to control the maximum size that the database can grow to, will find this feature useful.

## __Configuration__

To enable a max size limit of the in-memory database stores, please set the `MaxSizeBytes` property in the `StoreLimits` configuration section, of the `StoreAndForwardConfiguration` desired property element of Edge Hub, to the maximum size in bytes that you wish to limit the database store size to.
There are some other configuration changes that need to be made to enable this feature successfully:

### __Required configuration__

* The environment variable `UsePersistentStorage` has to be set to `false` for this feature to take any effect. *There's currently work in progress to enable the store size limits for the disk-based database store too. Once that work is complete, this configuration will not be required anymore*

When a maximum size limit for the database store is specified, Edge Hub will ensure that the message store size doesn't exceeded the specified limit before attempting to add a new item. If the message store size does exceed the specified limit, Edge Hub will stop accepting new messages from other modules and throw a `StorageFullException`.
Only when the message store decreases in size will Edge Hub start accepting new messages. The size of the message store can reduce due to successful message delivery to IoT hub or on TTL expiry.

## __Example__

### __How to set a store size limit__

Here's an example of how to set the store size limit for Edge Hub through Az CLI:

Create a deployment manifest `deployment.json` JSON file that has your IoT Edge deployment specification. More information about the IoT Edge deployment manifest (including a sample manifest that describes the `edgeHub` configuration section of the manifest) can be found [here][1].

The feature is specifically controlled using the following section of the `edgeHub` configuration in the deployment manifest:

```JSON
"storeAndForwardConfiguration": {
    "timeToLiveSecs": 7200,
    "storeLimits": {
        "maxSizeBytes": 1000000
    }
}
```

To deploy the deployment.json file to your device, you can use the Az CLI ([How to install][2]) with the Az CLI IoT Extension.

How to install the Az CLI IoT extension:

```bash
az extension add --name azure-cli-iot-ext
```

Deploy the deployment manifest created to your device:

```bash
az iot edge set-modules --device-id <your device ID> --hub-name <your hub name> --content .\deployment.json
```

The Edge Hub module running on your device should now start honoring the store size limit of 1000000 bytes as per your deployment manifest.

### __How to remove the size limit__

The store size limit can be cleared by modifying the deployment manifest to remove the `storeLimits` section entirely and issuing another IoT Edge deployment to your device using the new manifest.

Here's what the `storeAndForwardconfiguration` section of the deployment manifest should look like to clear the `storeLimits` specified earlier:

```JSON
"storeAndForwardConfiguration": {
    "timeToLiveSecs": 7200
}
```

[1]: https://docs.microsoft.com/azure/iot-edge/module-composition
[2]: https://docs.microsoft.com/cli/azure/install-azure-cli?view=azure-cli-latest