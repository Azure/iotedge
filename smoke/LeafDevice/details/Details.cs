// Copyright (c) Microsoft. All rights reserved.

namespace LeafDevice.Details
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LeafDevice.details;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using DeviceClientTransportType = Microsoft.Azure.Devices.Client.TransportType;
    using EventHubClientTransportType = Microsoft.Azure.EventHubs.TransportType;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;
    using Message = Microsoft.Azure.Devices.Client.Message;
    using ServiceClientTransportType = Microsoft.Azure.Devices.TransportType;

    public class Details
    {
        readonly string iothubConnectionString;
        readonly string eventhubCompatibleEndpointWithEntityPath;
        readonly string deviceId;
        readonly string certificateFileName;
        readonly string edgeHostName;
        readonly ServiceClientTransportType serviceClientTransportType;
        readonly EventHubClientTransportType eventHubClientTransportType;
        readonly ITransportSettings[] deviceTransportSettings;

        DeviceContext context;

        protected Details(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string certificateFileName,
            string edgeHostName,
            bool useWebSockets
        )
        {
            this.iothubConnectionString = iothubConnectionString;
            this.eventhubCompatibleEndpointWithEntityPath = eventhubCompatibleEndpointWithEntityPath;
            this.deviceId = deviceId;
            this.certificateFileName = certificateFileName;
            this.edgeHostName = edgeHostName;

            if (useWebSockets)
            {
                this.serviceClientTransportType = ServiceClientTransportType.Amqp_WebSocket_Only;
                this.eventHubClientTransportType = EventHubClientTransportType.AmqpWebSockets;
                this.deviceTransportSettings = new ITransportSettings[] { new MqttTransportSettings(DeviceClientTransportType.Mqtt_WebSocket_Only) };
            }
            else
            {
                this.serviceClientTransportType = ServiceClientTransportType.Amqp;
                this.eventHubClientTransportType = EventHubClientTransportType.Amqp;
                this.deviceTransportSettings = new ITransportSettings[] { new MqttTransportSettings(DeviceClientTransportType.Mqtt_Tcp_Only) };
            }
        }

        protected Task InstallCaCertificate()
        {
            // Since Windows will pop up security warning when add certificate to current user store location;
            // Therefore we will use CustomCertificateValidator instead.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Add(this.GetCertificate());
                store.Close();
            }

            return Task.CompletedTask;
        }

        X509Certificate2 GetCertificate() => new X509Certificate2(X509Certificate.CreateFromCertFile(this.certificateFileName));

        protected async Task ConnectToEdgeAndSendData()
        {
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            string leafDeviceConnectionString = $"HostName={builder.HostName};DeviceId={this.deviceId};SharedAccessKey={this.context.Device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={this.edgeHostName}";

            // Need to use CustomCertificateValidator since we can't automate to install certificate on Windows
            // For details, refer to InstallCaCertificate method
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !string.IsNullOrEmpty(this.certificateFileName))
            {
                // This will hook up callback on device transport settings to validate with given certificate
                CustomCertificateValidator.Create(new List<X509Certificate2> { this.GetCertificate() }, this.deviceTransportSettings);
            }

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(leafDeviceConnectionString, this.deviceTransportSettings);
            this.context.DeviceClientInstance = Option.Some(deviceClient);
            Console.WriteLine("Leaf Device client created.");

            var message = new Message(Encoding.ASCII.GetBytes($"Message from Leaf Device. MsgGUID: {this.context.MessageGuid}"));
            Console.WriteLine($"Trying to send the message to '{this.edgeHostName}'");

            await deviceClient.SendEventAsync(message);
            Console.WriteLine("Message Sent.");
            await deviceClient.SetMethodHandlerAsync("DirectMethod", DirectMethod, null).ConfigureAwait(false);
            Console.WriteLine("Direct method callback is set.");
        }

        protected async Task GetOrCreateDeviceIdentity()
        {
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString());

            Device device = await rm.GetDeviceAsync(this.deviceId);

            if (device != null)
            {
                Console.WriteLine($"Device '{device.Id}' already registered on IoT hub '{builder.HostName}'");

                this.context = new DeviceContext
                {
                    Device = device,
                    IotHubConnectionString = this.iothubConnectionString,
                    RegistryManager = rm,
                    RemoveDevice = false,
                    MessageGuid = Guid.NewGuid().ToString()
                };
            }
            else
            {
                await this.CreateDeviceIdentity(rm);
            }
        }

        async Task CreateDeviceIdentity(RegistryManager rm)
        {
            var device = new Device(this.deviceId)
            {
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas },
                Capabilities = new DeviceCapabilities() { IotEdge = false }
            };

            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            Console.WriteLine($"Registering device '{device.Id}' on IoT hub '{builder.HostName}'");

            device = await rm.AddDeviceAsync(device);

            this.context = new DeviceContext
            {
                Device = device,
                DeviceClientInstance = Option.None<DeviceClient>(),
                IotHubConnectionString = this.iothubConnectionString,
                RegistryManager = rm,
                RemoveDevice = true,
                MessageGuid = Guid.NewGuid().ToString()
            };
        }

        protected async Task VerifyDataOnIoTHub()
        {
            var builder = new EventHubsConnectionStringBuilder(this.eventhubCompatibleEndpointWithEntityPath);
            builder.TransportType = this.eventHubClientTransportType;

            Console.WriteLine($"Receiving events from device '{this.context.Device.Id}' on Event Hub '{builder.EntityPath}'");

            EventHubClient eventHubClient =
                EventHubClient.CreateFromConnectionString(builder.ToString());

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                "$Default",
                EventHubPartitionKeyResolver.ResolveToPartition(
                    this.context.Device.Id,
                    (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnqueuedTime(DateTime.Now.AddMinutes(-5)));

            var result = new TaskCompletionSource<bool>();
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            {
                using (cts.Token.Register(() => result.TrySetCanceled()))
                {
                    eventHubReceiver.SetReceiveHandler(
                        new PartitionReceiveHandler(
                            eventData =>
                            {
                                eventData.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);

                                if (devId != null && devId.ToString().Equals(this.context.Device.Id)
                                    && Encoding.UTF8.GetString(eventData.Body).Contains(this.context.MessageGuid))
                                {
                                    result.TrySetResult(true);
                                    return true;
                                }

                                return false;
                            }));

                    await result.Task;
                }
            }

            await eventHubReceiver.CloseAsync();
            await eventHubClient.CloseAsync();
        }

        static Task<MethodResponse> DirectMethod(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"Leaf device received direct method call...Payload Received: {methodRequest.DataAsJson}");
            return Task.FromResult(new MethodResponse(methodRequest.Data, (int)HttpStatusCode.OK));
        }

        protected async Task VerifyDirectMethod()
        {
            //User Service SDK to invoke Direct Method on the device.
            ServiceClient serviceClient =
                ServiceClient.CreateFromConnectionString(this.context.IotHubConnectionString, this.serviceClientTransportType);

            //Call a direct method
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300)))
            {
                CloudToDeviceMethod cloudToDeviceMethod = new CloudToDeviceMethod("DirectMethod").SetPayloadJson("{\"TestKey\" : \"TestValue\"}");

                CloudToDeviceMethodResult result = await serviceClient.InvokeDeviceMethodAsync(
                    this.context.Device.Id,
                    cloudToDeviceMethod,
                    cts.Token);

                if (result.Status != 200)
                {
                    throw new Exception("Could not invoke Direct Method on Device.");
                }
                else if (!result.GetPayloadAsJson().Equals("{\"TestKey\":\"TestValue\"}"))
                {
                    throw new Exception($"Payload doesn't match with Sent Payload. Received payload: {result.GetPayloadAsJson()}. Expected: {{\"TestKey\":\"TestValue\"}}");
                }
            }
        }

        protected void KeepDeviceIdentity()
        {
            if (this.context != null)
            {
                this.context.RemoveDevice = false;
            }
        }

        protected Task MaybeDeleteDeviceIdentity()
        {
            if (this.context != null)
            {
                Device device = this.context.Device;
                bool remove = this.context.RemoveDevice;
                this.context.Device = null;

                if (remove)
                {
                    return this.context.RegistryManager.RemoveDeviceAsync(device);
                }
            }

            return Task.CompletedTask;
        }
    }

    public class DeviceContext
    {
        public Device Device { get; set; }

        public Option<DeviceClient> DeviceClientInstance { get; set; }

        public string IotHubConnectionString { get; set; }

        public RegistryManager RegistryManager { get; set; }

        public bool RemoveDevice { get; set; }

        public string MessageGuid { get; set; } //used to identify exactly which message got sent.
    }
}
