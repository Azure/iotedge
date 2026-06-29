// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Identity;
    using global::Azure.Messaging.EventHubs;
    using global::Azure.Messaging.EventHubs.Consumer;
    using global::Azure.Messaging.EventHubs.Primitives;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Serilog;
    using DeviceTransportType = Microsoft.Azure.Devices.TransportType;

    public class IotHub
    {
        readonly AzureCliCredential credential;
        readonly string eventHubName;
        readonly string eventHubNamespace;
        readonly Lazy<Task<int>> eventHubPartitionCount;
        readonly string iotHubHostname;
        readonly Lazy<RegistryManager> registryManager;
        readonly Lazy<ServiceClient> serviceClient;
        static readonly TimeSpan eventHubRequestDuration = TimeSpan.FromSeconds(20);

        static AzureCliCredential CreateAzureCliCredential()
        {
            if (OsPlatform.IsArm() && OsPlatform.Is32Bit())
            {
                return new AzureCliCredential(new AzureCliCredentialOptions
                {
                    ProcessTimeout = TimeSpan.FromSeconds(60)
                });
            }

            return new AzureCliCredential();
        }

        public IotHub(string iotHubHostname, string eventHubName, string eventHubNamespace, Option<Uri> proxyUri)
        {
            this.credential = IotHub.CreateAzureCliCredential();
            this.eventHubName = eventHubName;
            this.eventHubNamespace = eventHubNamespace;
            this.iotHubHostname = iotHubHostname;
            Option<IWebProxy> proxy = proxyUri.Map(p => new WebProxy(p) as IWebProxy);

            this.registryManager = new Lazy<RegistryManager>(
                () =>
                {
                    var settings = new HttpTransportSettings();
                    proxy.ForEach(p => settings.Proxy = p);
                    return RegistryManager.Create(
                        this.iotHubHostname,
                        this.credential,
                        settings);
                });

            this.serviceClient = new Lazy<ServiceClient>(
                () =>
                {
                    var settings = new ServiceClientTransportSettings();
                    proxy.ForEach(p => settings.HttpProxy = p);
                    return ServiceClient.Create(
                        this.iotHubHostname,
                        this.credential,
                        DeviceTransportType.Amqp_WebSocket_Only,
                        settings);
                });

            this.eventHubPartitionCount = new Lazy<Task<int>>(
                () =>
                {
                    var consumerOptions = new EventHubConsumerClientOptions();
                    proxy.ForEach(p =>
                    {
                        consumerOptions.ConnectionOptions.TransportType = EventHubsTransportType.AmqpWebSockets;
                        consumerOptions.ConnectionOptions.Proxy = p;
                    });

                    var consumer = new EventHubConsumerClient(
                        EventHubConsumerClient.DefaultConsumerGroupName,
                        this.eventHubNamespace,
                        this.eventHubName,
                        this.credential,
                        consumerOptions);

                    return consumer.GetPartitionIdsAsync()
                        .ContinueWith(t => t.Result.Length)
                        .ContinueWith(t =>
                        {
                            Task.WhenAll(consumer.CloseAsync(), t);
                            return t.Result;
                        });
                });
        }

        public string Hostname => this.iotHubHostname;

        public string EventHubName => this.eventHubName;

        RegistryManager RegistryManager => this.registryManager.Value;

        ServiceClient ServiceClient => this.serviceClient.Value;

        public Task<Device> GetDeviceIdentityAsync(string deviceId, CancellationToken token) =>
            this.RegistryManager.GetDeviceAsync(deviceId, token);

        public async Task<Device> CreateDeviceIdentityAsync(Device device, CancellationToken token)
        {
            return await this.RegistryManager.AddDeviceAsync(device, token);
        }

        public async Task<Device> CreateEdgeDeviceIdentityAsync(string deviceId, Option<string> parentDeviceId, AuthenticationType authType, X509Thumbprint x509Thumbprint, CancellationToken token)
        {
            Device edge = await parentDeviceId.Match(
            async p =>
            {
                Device parentDevice = await this.GetDeviceIdentityAsync(p, token);
                string parentDeviceScope = parentDevice == null ? string.Empty : parentDevice.Scope;
                Log.Verbose($"Parent scope: {parentDeviceScope}");
                var result = new Device(deviceId)
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
                result.ParentScopes.Add(parentDeviceScope);
                return result;
            },
            () =>
            {
                return Task.FromResult(new Device(deviceId)
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
                });
            });

            return await this.CreateDeviceIdentityAsync(edge, token);
        }

        public Task DeleteDeviceIdentityAsync(Device device, CancellationToken token) =>
            this.RegistryManager.RemoveDeviceAsync(device.Id, token);

        public Task DeployDeviceConfigurationAsync(
            string deviceId,
            ConfigurationContent config,
            CancellationToken token) => this.RegistryManager.ApplyConfigurationContentOnDeviceAsync(deviceId, config, token);

        public Task<Twin> GetTwinAsync(
            string deviceId,
            Option<string> moduleId,
            CancellationToken token)
        {
            return moduleId.Match(
                m => this.RegistryManager.GetTwinAsync(deviceId, m, token),
                () => this.RegistryManager.GetTwinAsync(deviceId, token));
        }

        public async Task UpdateTwinAsync(
            string deviceId,
            string moduleId,
            object twinPatch,
            CancellationToken token)
        {
            Twin twin = await this.GetTwinAsync(deviceId, Option.Some(moduleId), token);
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
                e => !(e is DeviceNotFoundException) || ((DeviceNotFoundException)e).IsTransient,
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

                    // No Need to retry when server returns Bad Request.
                    return result.Status == 200 || result.Status == 400;
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
            seekTime = seekTime.ToUniversalTime().Subtract(TimeSpan.FromMinutes(2)); // substract 2 minutes to account for client/server drift

            var receiver = new PartitionReceiver(
                EventHubConsumerClient.DefaultConsumerGroupName,
                EventHubPartitionKeyResolver.ResolveToPartition(deviceId, await this.eventHubPartitionCount.Value),
                EventPosition.FromEnqueuedTime(seekTime),
                this.eventHubNamespace,
                this.eventHubName,
                this.credential);

            var result = new TaskCompletionSource<bool>();
            using (token.Register(() => result.TrySetCanceled()))
            {
                try
                {
                    while (!token.IsCancellationRequested && !result.Task.IsCompleted)
                    {
                        var batch = await receiver.ReceiveBatchAsync(50, token);
                        foreach (EventData eventData in batch)
                        {
                            if (onEventReceived(eventData))
                            {
                                result.TrySetResult(true);
                                break;
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // This is expected when the service is stopping.
                }
                finally
                {
                    await receiver.CloseAsync();
                }
            }

            await receiver.CloseAsync();
        }

        public async Task UpdateEdgeEnableStatus(string deviceId, bool enabled)
        {
            var edge = await this.RegistryManager.GetDeviceAsync(deviceId);

            if (!edge.Capabilities.IotEdge)
            {
                throw new ArgumentException($"{deviceId} is not an Edge device!");
            }

            edge.Status = enabled ? DeviceStatus.Enabled : DeviceStatus.Disabled;
            var updated = await this.RegistryManager.UpdateDeviceAsync(edge);
            Log.Verbose($"Updated enabled status for {deviceId}, enabled: {enabled}");
            Log.Verbose($"{updated.Id}, enabled: {updated.Status}");
        }

        class CatchTimeoutErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => ex is TaskCanceledException || ex is TimeoutException;
        }
    }
}
