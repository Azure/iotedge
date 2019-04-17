// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace common
{
    public class IotHub
    {
        private string hubConnectionString;

        public string Hostname =>
            IotHubConnectionStringBuilder
                .Create(this.hubConnectionString)
                .HostName;
        RegistryManager RegistryManager =>
            RegistryManager.CreateFromConnectionString(
                this.hubConnectionString,
                new HttpTransportSettings()
            );
        ServiceClient ServiceClient =>
            ServiceClient.CreateFromConnectionString(
                this.hubConnectionString,
                TransportType.Amqp_WebSocket_Only,
                new ServiceClientTransportSettings()
            );

        public IotHub(string hubConnectionString)
        {
            this.hubConnectionString = hubConnectionString;
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
   }
}