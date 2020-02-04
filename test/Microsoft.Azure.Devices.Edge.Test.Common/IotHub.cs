// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;
    using Serilog;
    using DeviceTransportType = Microsoft.Azure.Devices.TransportType;
    using EventHubTransportType = Microsoft.Azure.EventHubs.TransportType;

    public class IotHub
    {
        readonly string eventHubEndpoint;
        readonly string iotHubConnectionString;
        readonly Lazy<RegistryManager> registryManager;
        readonly Lazy<ServiceClient> serviceClient;
        readonly Lazy<EventHubClient> eventHubClient;

        public IotHub(string iotHubConnectionString, string eventHubEndpoint, Option<Uri> proxyUri)
        {
            this.eventHubEndpoint = eventHubEndpoint;
            this.iotHubConnectionString = iotHubConnectionString;
            Option<IWebProxy> proxy = proxyUri.Map(p => new WebProxy(p) as IWebProxy);

            this.registryManager = new Lazy<RegistryManager>(
                () =>
                {
                    var settings = new HttpTransportSettings();
                    proxy.ForEach(p => settings.Proxy = p);
                    return RegistryManager.CreateFromConnectionString(
                        this.iotHubConnectionString,
                        settings);
                });

            this.serviceClient = new Lazy<ServiceClient>(
                () =>
                {
                    var settings = new ServiceClientTransportSettings();
                    proxy.ForEach(p => settings.HttpProxy = p);
                    return ServiceClient.CreateFromConnectionString(
                        this.iotHubConnectionString,
                        DeviceTransportType.Amqp_WebSocket_Only,
                        settings);
                });

            this.eventHubClient = new Lazy<EventHubClient>(
                () =>
                {
                    var builder = new EventHubsConnectionStringBuilder(this.eventHubEndpoint)
                    {
                        TransportType = EventHubTransportType.AmqpWebSockets
                    };
                    var client = EventHubClient.CreateFromConnectionString(builder.ToString());
                    proxy.ForEach(p => client.WebProxy = p);
                    return client;
                });
        }

        public string Hostname =>
            IotHubConnectionStringBuilder.Create(this.iotHubConnectionString).HostName;

        public string EntityPath =>
            new EventHubsConnectionStringBuilder(this.eventHubEndpoint).EntityPath;

        RegistryManager RegistryManager => this.registryManager.Value;

        ServiceClient ServiceClient => this.serviceClient.Value;

        EventHubClient EventHubClient => this.eventHubClient.Value;

        public Task<Device> GetDeviceIdentityAsync(string deviceId, CancellationToken token) =>
            this.RegistryManager.GetDeviceAsync(deviceId, token);

        public async Task<Device> CreateDeviceIdentityAsync(Device device, CancellationToken token)
        {
            return await this.RegistryManager.AddDeviceAsync(device, token);
        }

        public Task<Device> CreateEdgeDeviceIdentityAsync(string deviceId, AuthenticationType authType, X509Thumbprint x509Thumbprint, CancellationToken token)
        {
            Device edge = new Device(deviceId)
            {
                Authentication = new AuthenticationMechanism()
                {
                    Type = authType,
                    X509Thumbprint = x509Thumbprint
                },
                Capabilities = new DeviceCapabilities()
                {
                    IotEdge = true
                }
            };

            return this.CreateDeviceIdentityAsync(edge, token);
        }

        public Task DeleteDeviceIdentityAsync(Device device, CancellationToken token) =>
            this.RegistryManager.RemoveDeviceAsync(device);

        public Task DeployDeviceConfigurationAsync(
            string deviceId,
            ConfigurationContent config,
            CancellationToken token) => this.RegistryManager.ApplyConfigurationContentOnDeviceAsync(deviceId, config, token);

        public Task<Twin> GetTwinAsync(
            string deviceId,
            string moduleId,
            CancellationToken token) => this.RegistryManager.GetTwinAsync(deviceId, moduleId, token);

        public async Task UpdateTwinAsync(
            string deviceId,
            string moduleId,
            object twinPatch,
            CancellationToken token)
        {
            Twin twin = await this.GetTwinAsync(deviceId, moduleId, token);
            string patch = JsonConvert.SerializeObject(twinPatch);
            await this.RegistryManager.UpdateTwinAsync(
                deviceId,
                moduleId,
                patch,
                twin.ETag,
                token);
        }

        public Task<CloudToDeviceMethodResult> InvokeMethodAsync(
            string deviceId,
            CloudToDeviceMethod method,
            CancellationToken token)
        {
            return Retry.Do(
                () => this.ServiceClient.InvokeDeviceMethodAsync(deviceId, method, token),
                result => result.Status == 200,
                e => true,
                TimeSpan.FromSeconds(5),
                token);
        }

        public Task<CloudToDeviceMethodResult> InvokeMethodAsync(
            string deviceId,
            string moduleId,
            CloudToDeviceMethod method,
            CancellationToken token)
        {
            return Retry.Do(
                () => this.ServiceClient.InvokeDeviceMethodAsync(deviceId, moduleId, method, token),
                result =>
                {
                    Log.Verbose($"Method '{method.MethodName}' on '{deviceId}/{moduleId}' returned: " +
                        $"{result.Status}\n{result.GetPayloadAsJson()}");
                    return result.Status == 200;
                },
                e =>
                    {
                        Log.Verbose($"Exception: {e}");
                        return true;
                    },
                TimeSpan.FromSeconds(5),
                token);
        }

        public async Task ReceiveEventsAsync(
            string deviceId,
            DateTime seekTime,
            Func<EventData, bool> onEventReceived,
            CancellationToken token)
        {
            EventHubClient client = this.EventHubClient;
            int count = (await client.GetRuntimeInformationAsync()).PartitionCount;
            string partition = EventHubPartitionKeyResolver.ResolveToPartition(deviceId, count);
            seekTime = seekTime.ToUniversalTime().Subtract(TimeSpan.FromMinutes(2)); // substract 2 minutes to account for client/server drift
            EventPosition position = EventPosition.FromEnqueuedTime(seekTime);
            PartitionReceiver receiver = client.CreateReceiver("$Default", partition, position);

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
                        }));

                await result.Task;
            }

            await receiver.CloseAsync();
        }
    }
}
