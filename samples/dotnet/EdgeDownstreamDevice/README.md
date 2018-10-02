# Send Event Sample

Sample application that uses the Azure IoT Dotnet SDK to send telemetry messages to the
Azure IoT Hub cloud service or to an Azure IoT Edge device. The sample demonstrates how to connect
and send messages using a protocol of your choices as a parameter.

## Build the sample

```
$> cd EdgeDownstreamDevice
$> dotnet build
```

## Run the sample

Before running the sample, edit the file Properties/launchSettings.json and add the connection string and CA certificate

### Configuration Description

* Connection String:

  * IoT Edge connection string:

    ```
    HostName=your-hub.azure-devices.net;DeviceId=yourDevice;SharedAccessKey=XXXYYYZZZ=;GatewayHostName=mygateway.contoso.com
    ```
  * Just for reference, here is the IoT Hub connection string format:

    ```
    HostName=your-hub.azure-devices.net;DeviceId=yourDevice;SharedAccessKey=XXXYYYZZZ=;
    ```

* Number of messages to send - Expressed in decimal
* Path to trusted CA certificate: For the Edge Hub, if the CA is not a public root, a path tp the root CA certificate in PEM format is absolutely required. This is optional if the root certificate is installed in the trusted certificate store of the OS.

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