// Copyright (c) Microsoft. All rights reserved.

namespace common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;

    using DeviceTransportType = Microsoft.Azure.Devices.TransportType;
    using EventHubTransportType = Microsoft.Azure.EventHubs.TransportType;

    public class IotHub
    {
        readonly string eventHubEndpoint;
        readonly string iotHubConnectionString;

        public string Hostname =>
            IotHubConnectionStringBuilder.Create(this.iotHubConnectionString).HostName;
        public string EntityPath =>
            (new EventHubsConnectionStringBuilder(this.eventHubEndpoint)).EntityPath;

        RegistryManager RegistryManager =>
            RegistryManager.CreateFromConnectionString(
                this.iotHubConnectionString,
                new HttpTransportSettings()
            );
        ServiceClient ServiceClient =>
            ServiceClient.CreateFromConnectionString(
                this.iotHubConnectionString,
                DeviceTransportType.Amqp_WebSocket_Only,
                new ServiceClientTransportSettings()
            );

        public IotHub(string iotHubConnectionString, string eventHubEndpoint)
        {
            this.eventHubEndpoint =
                Preconditions.CheckNonWhiteSpace(eventHubEndpoint, nameof(eventHubEndpoint));
            this.iotHubConnectionString =
                Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString));
        }

        public Task<Device> GetDeviceIdentityAsync(string deviceId, CancellationToken token) =>
            this.RegistryManager.GetDeviceAsync(deviceId, token);

        public Task<Device> CreateEdgeDeviceIdentity(string deviceId, CancellationToken token)
        {
            var device = new Device(deviceId)
            {
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas },
                Capabilities = new DeviceCapabilities() { IotEdge = true }
            };
            return this.RegistryManager.AddDeviceAsync(device, token);
        }

        public Task DeleteDeviceIdentityAsync(Device device, CancellationToken token) =>
            this.RegistryManager.RemoveDeviceAsync(device);
 
         public Task DeployDeviceConfigurationAsync(
            string deviceId,
            ConfigurationContent config,
            CancellationToken token
        ) => this.RegistryManager.ApplyConfigurationContentOnDeviceAsync(deviceId, config, token);

        public Task<Twin> GetTwinAsync(
            string deviceId,
            string moduleId,
            CancellationToken token
        ) => this.RegistryManager.GetTwinAsync(deviceId, moduleId, token);

        public async Task UpdateTwinAsync(
            string deviceId,
            string moduleId,
            object twinPatch,
            CancellationToken token
        )
        {
            Twin twin = await this.GetTwinAsync(deviceId, moduleId, token);
            string patch = JsonConvert.SerializeObject(twinPatch);
            await this.RegistryManager.UpdateTwinAsync(
                deviceId, moduleId, patch, twin.ETag, token);
        }

        public Task<CloudToDeviceMethodResult> InvokeMethodAsync(
            string deviceId,
            string moduleId,
            CloudToDeviceMethod method,
            CancellationToken token
        )
        {
            return this.ServiceClient.InvokeDeviceMethodAsync(deviceId, moduleId, method, token);
        }

        public async Task ReceiveEventsAsync(
            string deviceId,
            Func<EventData, bool> onEventReceived,
            CancellationToken token
        )
        {
            var builder = new EventHubsConnectionStringBuilder(this.eventHubEndpoint)
            {
                TransportType = EventHubTransportType.AmqpWebSockets
            };

            EventHubClient client = EventHubClient.CreateFromConnectionString(builder.ToString());
            int count = (await client.GetRuntimeInformationAsync()).PartitionCount;
            string partition = EventHubPartitionKeyResolver.ResolveToPartition(deviceId, count);
            PartitionReceiver receiver = client.CreateReceiver("$Default", partition, EventPosition.FromEnd());

            var result = new TaskCompletionSource<bool>();
            using (token.Register(() => result.TrySetCanceled()))
            {
                receiver.SetReceiveHandler(
                    new PartitionReceiveHandler(
                        data =>
                        {
                            bool done = onEventReceived(data);
                            if (done)
                            {
                                result.TrySetResult(true);
                            }
                            return done;
                        }
                    )
                );

                await result.Task;
            }

            await receiver.CloseAsync();
            await client.CloseAsync();
        }
   }
}