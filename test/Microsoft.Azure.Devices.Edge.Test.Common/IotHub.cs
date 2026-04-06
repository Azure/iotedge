// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Serilog;
    using EventHubTransportType = Microsoft.Azure.EventHubs.TransportType;
    using RetryPolicy = Util.TransientFaultHandling.RetryPolicy;

    public class IotHub
    {
        readonly string eventHubEndpoint;
        readonly string iotHubConnectionString;
        readonly Lazy<IotHubServiceClient> serviceClient;
        readonly Lazy<EventHubClient> eventHubClient;
        static readonly TimeSpan eventHubRequestDuration = TimeSpan.FromSeconds(20);

        public IotHub(string iotHubConnectionString, string eventHubEndpoint, Option<Uri> proxyUri)
        {
            this.eventHubEndpoint = eventHubEndpoint;
            this.iotHubConnectionString = iotHubConnectionString;
            Option<IWebProxy> proxy = proxyUri.Map(p => new WebProxy(p) as IWebProxy);

            this.serviceClient = new Lazy<IotHubServiceClient>(
                () => new IotHubServiceClient(this.iotHubConnectionString));

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
            ParseConnectionString(this.iotHubConnectionString)["HostName"];

        public string EntityPath =>
            new EventHubsConnectionStringBuilder(this.eventHubEndpoint).EntityPath;

        IotHubServiceClient ServiceClient => this.serviceClient.Value;

        EventHubClient EventHubClient => this.eventHubClient.Value;

        public Task<Device> GetDeviceIdentityAsync(string deviceId, CancellationToken token) =>
            this.ServiceClient.Devices.GetAsync(deviceId, token);

        public async Task<Device> CreateDeviceIdentityAsync(Device device, CancellationToken token)
        {
            return await this.ServiceClient.Devices.CreateAsync(device, token);
        }

        public async Task<Device> CreateEdgeDeviceIdentityAsync(string deviceId, Option<string> parentDeviceId, ClientAuthenticationType authType, X509Thumbprint x509Thumbprint, CancellationToken token)
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
                    Capabilities = new ClientCapabilities()
                    {
                        IsIotEdge = true
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
                    Capabilities = new ClientCapabilities()
                    {
                        IsIotEdge = true
                    }
                });
            });

            return await this.CreateDeviceIdentityAsync(edge, token);
        }

        public Task DeleteDeviceIdentityAsync(Device device, CancellationToken token) =>
            this.ServiceClient.Devices.DeleteAsync(device.Id);

        public Task DeployDeviceConfigurationAsync(
            string deviceId,
            ConfigurationContent config,
            CancellationToken token) => this.ServiceClient.Configurations.ApplyConfigurationContentOnDeviceAsync(deviceId, config, token);

        public Task<ClientTwin> GetTwinAsync(
            string deviceId,
            Option<string> moduleId,
            CancellationToken token)
        {
            return moduleId.Match(
                m => this.ServiceClient.Twins.GetAsync(deviceId, m, token),
                () => this.ServiceClient.Twins.GetAsync(deviceId, token));
        }

        public async Task UpdateTwinAsync(
            string deviceId,
            string moduleId,
            object twinPatch,
            CancellationToken token)
        {
            ClientTwin twin = await this.GetTwinAsync(deviceId, Option.Some(moduleId), token);
            ClientTwin patch = new ClientTwin();
            foreach (var prop in JObject.FromObject(twinPatch).Properties())
            {
                patch.Properties.Desired[prop.Name] = prop.Value;
            }

            await this.ServiceClient.Twins.UpdateAsync(
                deviceId,
                moduleId,
                patch,
                true,
                token);
        }

        public Task<DirectMethodClientResponse> InvokeMethodAsync(
            string deviceId,
            DirectMethodServiceRequest method,
            CancellationToken token)
        {
            return Retry.Do(
                () => this.ServiceClient.DirectMethods.InvokeAsync(deviceId, method, token),
                result => result.Status == 200,
                e => !(e is IotHubServiceException),
                TimeSpan.FromSeconds(5),
                token);
        }

        public Task<DirectMethodClientResponse> InvokeMethodAsync(
            string deviceId,
            string moduleId,
            DirectMethodServiceRequest method,
            CancellationToken token)
        {
            return Retry.Do(
                () => this.ServiceClient.DirectMethods.InvokeAsync(deviceId, moduleId, method, token),
                result =>
                {
                    Log.Verbose($"Method '{method.MethodName}' on '{deviceId}/{moduleId}' returned: " +
                        $"{result.Status}\n{result.JsonPayload}");

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
            EventHubClient client = this.EventHubClient;
            int count = (await GetPartitionCountAsync(client, token)).PartitionCount;
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

        static async Task<EventHubRuntimeInformation> GetPartitionCountAsync(EventHubClient client, CancellationToken token)
        {
            CancellationToken eachRequestCancellationToken = new CancellationTokenSource(eventHubRequestDuration).Token;

            // Sometimes eventhub will hang when getting runtime information, so we need a retry.
            var retryStrategy = new Incremental(15, RetryStrategy.DefaultRetryInterval, RetryStrategy.DefaultRetryIncrement);
            var retryPolicy = new RetryPolicy(new CatchTimeoutErrorDetectionStrategy(), retryStrategy);
            return await retryPolicy.ExecuteAsync(
                async () =>
            {
                return await Task.Run(async () => await client.GetRuntimeInformationAsync(), eachRequestCancellationToken);
            }, token);
        }

        public async Task UpdateEdgeEnableStatus(string deviceId, bool enabled)
        {
            var edge = await this.ServiceClient.Devices.GetAsync(deviceId);

            if (!edge.Capabilities.IsIotEdge)
            {
                throw new ArgumentException($"{deviceId} is not an Edge device!");
            }

            edge.Status = enabled ? ClientStatus.Enabled : ClientStatus.Disabled;
            var updated = await this.ServiceClient.Devices.SetAsync(edge);
            Log.Verbose($"Updated enabled status for {deviceId}, enabled: {enabled}");
            Log.Verbose($"{updated.Id}, enabled: {updated.Status}");
        }

        class CatchTimeoutErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => ex is TaskCanceledException || ex is TimeoutException;
        }

        static Dictionary<string, string> ParseConnectionString(string connectionString) =>
            connectionString.Split(';')
                .Select(part => part.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
    }
}
