# Send Event Sample

Sample application that uses the Azure IoT Dotnet SDK to send telemetry messages to the
Azure IoT Edge gateway device. The sample demonstrates how to connect and send messages
using a protocol of your choices as a parameter.

## Build the sample

As a prerequisite you will need the [DotNet Core SDK](https://docs.microsoft.com/dotnet/core/sdk) installed on your dev box.
To build the sample, copy the contents of this directory on your dev box and follow the instructions below.

```
$> cd EdgeX509AuthDownstreamDevice
$> dotnet build
```

## Run the sample

Before running the sample, edit the file Properties/launchSettings.json and add the IoT and Edge Hub details along with all the certificates.

### Configuration Description

* IOTHUB_HOSTNAME: This is the hostname of the IoT Hub instance. For example your-hub.azure-devices.net

* IOTEDGE_GATEWAY_HOSTNAME: This is the hostname of the IoT Edge device that your downstream device will connect to. For example mygateway.contoso.com

* DEVICE_ID: This is the device id of the downstream device setup with X.509 auth.

* DEVICE_IDENTITY_X509_CERTIFICATE_PEM_PATH: Path to the identity X.509 certificate of the downstream device in PEM format. 

* DEVICE_IDENTITY_X509_CERTIFICATE_KEY_PEM_PATH: Path to the identity X.509 private key of the downstream device in PEM format. 

* IOTEDGE_TRUSTED_CA_CERTIFICATE_PEM_PATH: Path to the trusted CA in the certificate chain of the Edge device gateway.

* MESSAGE_COUNT: Number of messages to send - Expressed in decimal.

### Execute Sample

```
$> dotnet run
```

## Verify output

If everything was correctly provided via the CLI arguments, the following should be observed on stdout

```
Edge downstream device attempting to send 10 messages to Edge Hub...

        9/27/2018 3:25:57 PM> Sending message: 0, Data: [{MyFirstDownstreamDevice "messageId":0,"temperature":34,"humidity":75}]
        9/27/2018 3:26:05 PM> Sending message: 1, Data: [{MyFirstDownstreamDevice "messageId":1,"temperature":24,"humidity":76}]
        9/27/2018 3:26:05 PM> Sending message: 2, Data: [{MyFirstDownstreamDevice "messageId":2,"temperature":33,"humidity":64}]
        9/27/2018 3:26:05 PM> Sending message: 3, Data: [{MyFirstDownstreamDevice "messageId":3,"temperature":34,"humidity":74}]
        9/27/2018 3:26:05 PM> Sending message: 4, Data: [{MyFirstDownstreamDevice "messageId":4,"temperature":31,"humidity":64}]
        9/27/2018 3:26:05 PM> Sending message: 5, Data: [{MyFirstDownstreamDevice "messageId":5,"temperature":28,"humidity":76}]
        9/27/2018 3:26:05 PM> Sending message: 6, Data: [{MyFirstDownstreamDevice "messageId":6,"temperature":31,"humidity":62}]
        ...
```
