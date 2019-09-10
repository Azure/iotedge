# External provisioning in IoT Edge

IoT Edge supports a provisioning mode called `external` whereby the `iotedged` obtains the device's provisioning information by calling a REST API hosted on an HTTP endpoint.
The endpoint that is targeted by the `iotedged` is specified in IoT Edge's config.yaml file as follows:

TCP:

```yaml
provisioning:
  source: "external"
  endpoint: "http://localhost:9999"
```

Unix sockets:

```yaml
provisioning:
  source: "external"
  endpoint: "unix:///var/run/external/external.sock"
```

**Please note that HTTPS is currently not supported for this external endpoint.**

**Customers who would like to restrict access to the external endpoint to just whitelist `iotedged` can do so by hosting their HTTP endpoint on Unix sockets instead of using TCP and giving appropriate permissions to the `iotedged` to be able to access the socket.**

## Use case

Customers that have their devices partitioned (virtualized) in a way where `iotedged` runs on a partition that is isolated from the partition that hosts the device's provisioning information will
find this provisioning mode useful. Some customers prefer to keep the device's confidential information such as the device's connection string or identity X.509 certificate on a separate partition from where the
`iotedged` and IoT edge modules are running and they can leverage this feature to provide `iotedged` the provisioning information required to start execution.

When this provisioning mode is used, `iotedged` calls an API on the `endpoint` specified in the config.yaml file to retrieve the device's provisioning information as part of bootstrapping.

## REST API specification

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
    }
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
    }
}
```

For more information about the REST API's specification, please refer to the [endpoint's swagger specification](../edgelet/api/externalProvisioningVersion_2019_04_10.yaml)
