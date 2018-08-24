// Copyright (c) Microsoft. All rights reserved.

namespace LeafDevice.Details
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using Microsoft.Azure.Devices.Client;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;

    public class Details
    {
        readonly string iothubConnectionString;
        readonly string eventhubCompatibleEndpointWithEntityPath;
        readonly string deviceId;
        readonly string certificateFileName;
        readonly string edgeHostName;

        DeviceContext context;

        protected Details(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string certificateFileName,
            string edgeHostName
        )
        {
            this.iothubConnectionString = iothubConnectionString;
            this.eventhubCompatibleEndpointWithEntityPath = eventhubCompatibleEndpointWithEntityPath;
            this.deviceId = deviceId;
            this.certificateFileName = certificateFileName;
            this.edgeHostName = edgeHostName;
        }

        protected Task InstallCaCertificate()
        {
            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate.CreateFromCertFile(this.certificateFileName)));
            store.Close();
            return Task.CompletedTask;
        }

        protected async Task ConnectToEdgeAndSendData()
        {
            string leafDeviceConnectionString = this.iothubConnectionString + $";DeviceId={this.deviceId};gatewayHostName={this.edgeHostName}";
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(leafDeviceConnectionString, Microsoft.Azure.Devices.Client.TransportType.Mqtt);
            var message = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes("Message from Leaf Device."));

            await deviceClient.SendEventAsync(message);
        }

        protected async Task GetOrCreateDeviceIdentity()
        {
            Microsoft.Azure.Devices.IotHubConnectionStringBuilder builder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
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
                    RemoveDevice = false
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

            Microsoft.Azure.Devices.IotHubConnectionStringBuilder builder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            Console.WriteLine($"Registering device '{device.Id}' on IoT hub '{builder.HostName}'");

            device = await rm.AddDeviceAsync(device);

            this.context = new DeviceContext
            {
                Device = device,
                IotHubConnectionString = this.iothubConnectionString,
                RegistryManager = rm,
                RemoveDevice = true
            };
        }

        protected async Task VerifyDataOnIoTHub()
        {
            var builder = new EventHubsConnectionStringBuilder(this.eventhubCompatibleEndpointWithEntityPath);

            Console.WriteLine($"Receiving events from device '{this.context.Device.Id}' on Event Hub '{builder.EntityPath}'");

            EventHubClient eventHubClient =
                EventHubClient.CreateFromConnectionString(builder.ToString());

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                "$Default",
                EventHubPartitionKeyResolver.ResolveToPartition(
                    this.context.Device.Id,
                    (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                DateTime.Now);

            var result = new TaskCompletionSource<bool>();
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            {
                using (cts.Token.Register(() => result.TrySetCanceled()))
                {
                    eventHubReceiver.SetReceiveHandler(
                        new PartitionReceiveHandler(
                            eventData =>
                            {
                                eventData.Properties.TryGetValue("iothub-connection-device-id", out object devId);

                                if (devId != null && devId.ToString().Equals(this.context.Device.Id))
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
        public Device Device;
        public string IotHubConnectionString;
        public RegistryManager RegistryManager;
        public bool RemoveDevice;
    }
}
