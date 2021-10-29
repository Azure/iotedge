# External provisioning in IoT Edge

IoT Edge supports a provisioning mode called `external` whereby the `iotedged` obtains the device's provisioning information by calling a REST API hosted on an HTTP endpoint.
The endpoint that is targeted by the `iotedged` is specified in IoT Edge's config.yaml file as follows:

TCP:

```yaml
provisioning:
  source: "external"
  endpoint: "http://localhost:9999"
  dynamic_reprovisioning: false
```

Unix sockets:

```yaml
provisioning:
  source: "external"
  endpoint: "unix:///var/run/external/external.sock"
  dynamic_reprovisioning: false
```

**Please note that HTTPS is currently not supported for this external endpoint.**

**Customers who would like to restrict access to the external endpoint to just `iotedged` can do so by hosting their HTTP endpoint on Unix sockets instead of using TCP and giving appropriate permissions to the `iotedged` to be able to access the socket.**

## __Use case__

Customers that have their devices partitioned (virtualized) in a way where `iotedged` runs on a partition that is isolated from the partition that hosts the device's provisioning information will
find this provisioning mode useful. Some customers prefer to keep the device's confidential information such as the device's connection string or identity X.509 certificate on a separate partition from where the
`iotedged` and IoT edge modules are running and they can leverage this feature to provide `iotedged` the provisioning information required to start execution.

When this provisioning mode is used, `iotedged` calls an API on the `endpoint` specified in the config.yaml file to retrieve the device's provisioning information as part of bootstrapping.

### __Dynamic re-provisioning__

IoT Edge has the ability to detect a 'possible' re-provisioning of a device dynamically and when the `external` provisioning mode is used to provision the device, IoT Edge notifies the external endpoint
about the possibility of a device being re-provisioned to a different IoT Hub.

The notification is sent to the external endpoint by calling the `reprovision` API.
On receiving such a notification, the external endpoint can check whether the device has indeed been de-provisioned from it's original IoT Hub and provisioned on another IoT Hub instead.
The external endpoint can then take appropriate actions to reconfigure the device such as cleaning up any cached local state and restarting IoT Edge.

Upon restarting IoT Edge, `iotedged` will request the external endpoint for the device's provisioning information and the external endpoint can then return the new provisioning information
of the device along with the appropriate values for the `status` and `substatus` properties. These properties are used by `iotedged` as triggers to clean up any cached state from earlier, if required,
and proceed with regular execution henceforth.
The values of these properties are in accordance with the same properties defined by the Azure IoT Device Provisioning Service (DPS) which can be found [here][1].
The external endpoint should return appropriate values for these properties if it would like `iotedged` to perform any local state cleanup as part of device re-provisioning.

The dynamic re-provisioning feature can be enabled by setting the value of the `dynamic_reprovisioning` to `true` in the IoT Edge config.yaml file.

## __REST API specification__

### __Get Device Provisioning Information__

The REST API called by the `iotedged` on the external endpoint to retrieve the device's provisioning information has the following specification:

VERB: GET

PATH: /device/provisioninginformation

REQUEST PAYLOAD: None

RESPONSE PAYLOAD:

```json
{
  "hubName": "IoT Hub Name",
  "deviceId": "Device ID",
  "credentials": {
      "authType": "symmetric-key | x509",
      "source": "payload | hsm",
      "key": "The symmetric key used. Only populated for the `symmetric-key` authType",
      "identityCert": "PEM encoded identity certificate (for the x509 and payload mode) in base64 representation | Path to identity certificate (for the x509 and hsm mode)",
      "identityPrivateKey": "PEM encoded identity private key (for the x509 and payload mode) in base64 representation | Path to identity private key (for the x509 and hsm mode)"
    },
  "status": "The device's provisioning status in IoT Hub (Optional)",
  "substatus": "The device's provisioning sub-status in IoT Hub (Optional)"
}
```

A sample response for the `symmetric-key` `authType` with the `payload` specified as the credential's `source` would look like the following:

```json
{
  "hubName": "myHub1.azure-devices.net",
  "deviceId": "myDevice1",
  "credentials": {
      "authType": "symmetric-key",
      "source": "payload",
      "key": "bXlLZXkxMjM0NQ=="
    },
  "status": "assigned",
  "substatus": "initialAssignment"
}
```

### __Re-provision device__

The REST API called by the `iotedged` on the external endpoint to re-provision the device has the following specification:

VERB: POST

PATH: /device/reprovision

REQUEST PAYLOAD: None

RESPONSE PAYLOAD: None

For more information about the REST API's specification, please refer to the [endpoint's swagger specification](../edgelet/api/externalProvisioningVersion_2019_04_10.yaml)

[1]: https://docs.microsoft.com/rest/api/iot-dps/service/device-registration-state/query