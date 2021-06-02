// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static System.FormattableString;

    /// <summary>
    /// EdgeHubConnection is responsible for connecting to the cloud as the Edge hub and getting/updating the edge hub twin.
    /// </summary>
    public class EdgeHubConnection
    {
        readonly IIdentity edgeHubIdentity;
        readonly ITwinManager twinManager;
        readonly IMessageConverter<TwinCollection> twinCollectionMessageConverter;
        readonly VersionInfo versionInfo;
        readonly RouteFactory routeFactory;
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        Func<IMessage, Task> configUpdateCallback;

        internal EdgeHubConnection(
            IIdentity edgeHubIdentity,
            ITwinManager twinManager,
            RouteFactory routeFactory,
            IMessageConverter<TwinCollection> twinCollectionMessageConverter,
            VersionInfo versionInfo,
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            this.edgeHubIdentity = edgeHubIdentity;
            this.twinManager = twinManager;
            this.twinCollectionMessageConverter = twinCollectionMessageConverter;
            this.routeFactory = routeFactory;
            this.versionInfo = versionInfo ?? VersionInfo.Empty;
            this.deviceScopeIdentitiesCache = deviceScopeIdentitiesCache;
        }

        public static async Task<EdgeHubConnection> Create(
            IIdentity edgeHubIdentity,
            IEdgeHub edgeHub,
            ITwinManager twinManager,
            IConnectionManager connectionManager,
            RouteFactory routeFactory,
            IMessageConverter<TwinCollection> twinCollectionMessageConverter,
            VersionInfo versionInfo,
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            Preconditions.CheckNotNull(edgeHubIdentity, nameof(edgeHubIdentity));
            Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(twinCollectionMessageConverter, nameof(twinCollectionMessageConverter));
            Preconditions.CheckNotNull(routeFactory, nameof(routeFactory));
            Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));

            var edgeHubConnection = new EdgeHubConnection(
                edgeHubIdentity,
                twinManager,
                routeFactory,
                twinCollectionMessageConverter,
                versionInfo ?? VersionInfo.Empty,
                deviceScopeIdentitiesCache);

            await InitEdgeHub(edgeHubConnection, connectionManager, edgeHubIdentity, edgeHub);
            connectionManager.DeviceConnected += edgeHubConnection.DeviceConnected;
            connectionManager.DeviceDisconnected += edgeHubConnection.DeviceDisconnected;
            Events.Initialized(edgeHubIdentity);
            return edgeHubConnection;
        }

        internal void SetDesiredPropertiesUpdateCallback(Func<IMessage, Task> callback)
        {
            this.configUpdateCallback = callback;
        }

        internal async void DeviceDisconnected(object sender, IIdentity device)
        {
            try
            {
                await this.UpdateDeviceConnectionStatus(device, ConnectionStatus.Disconnected);
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingDeviceDisconnectedEvent(device, ex);
            }
        }

        internal async void DeviceConnected(object sender, IIdentity device)
        {
            try
            {
                await this.UpdateDeviceConnectionStatus(device, ConnectionStatus.Connected);
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingDeviceConnectedEvent(device, ex);
            }
        }

        internal async Task<DirectMethodResponse> HandleMethodInvocation(DirectMethodRequest request)
        {
            Preconditions.CheckNotNull(request, nameof(request));
            Events.MethodRequestReceived(request.Name);
            if (request.Name.Equals(Constants.ServiceIdentityRefreshMethodName, StringComparison.OrdinalIgnoreCase))
            {
                RefreshRequest refreshRequest;
                try
                {
                    refreshRequest = request.Data.FromBytes<RefreshRequest>();
                }
                catch (Exception e)
                {
                    Events.ErrorParsingMethodRequest(e);
                    return new DirectMethodResponse(e, HttpStatusCode.BadRequest);
                }

                try
                {
                    Events.RefreshingServiceIdentities(refreshRequest.DeviceIds);
                    await this.deviceScopeIdentitiesCache.RefreshServiceIdentities(refreshRequest.DeviceIds);
                    Events.RefreshedServiceIdentities(refreshRequest.DeviceIds);
                    return new DirectMethodResponse(request.CorrelationId, null, (int)HttpStatusCode.OK);
                }
                catch (Exception e)
                {
                    Events.ErrorRefreshingServiceIdentities(e);
                    return new DirectMethodResponse(e, HttpStatusCode.InternalServerError);
                }
            }
            else
            {
                Events.InvalidMethodRequest(request.Name);
                return new DirectMethodResponse(new InvalidOperationException($"Method {request.Name} is not supported"), HttpStatusCode.NotFound);
            }
        }

        static Task InitEdgeHub(EdgeHubConnection edgeHubConnection, IConnectionManager connectionManager, IIdentity edgeHubIdentity, IEdgeHub edgeHub)
        {
            IDeviceProxy deviceProxy = new EdgeHubDeviceProxy(edgeHubConnection);
            Task addDeviceConnectionTask = connectionManager.AddDeviceConnection(edgeHubIdentity, deviceProxy);
            Task desiredPropertyUpdatesSubscriptionTask = edgeHub.AddSubscription(edgeHubIdentity.Id, DeviceSubscription.DesiredPropertyUpdates);
            Task methodsSubscriptionTask = edgeHub.AddSubscription(edgeHubIdentity.Id, DeviceSubscription.Methods);
            Task clearDeviceConnectionStatusesTask = edgeHubConnection.ClearDeviceConnectionStatuses();
            return Task.WhenAll(addDeviceConnectionTask, desiredPropertyUpdatesSubscriptionTask, methodsSubscriptionTask, clearDeviceConnectionStatusesTask);
        }

        Task HandleDesiredPropertiesUpdate(IMessage desiredProperties)
        {
            return this.configUpdateCallback?.Invoke(desiredProperties);
        }

        Task UpdateDeviceConnectionStatus(IIdentity client, ConnectionStatus connectionStatus)
        {
            try
            {
                if (client.Id.Equals(this.edgeHubIdentity.Id))
                {
                    Events.SkipUpdatingEdgeHubIdentity(client.Id, connectionStatus);
                    return Task.CompletedTask;
                }

                Events.UpdatingDeviceConnectionStatus(client.Id, connectionStatus);

                // If a downstream device disconnects, then remove the entry from Reported properties
                // If a module disconnects, then update the entry to status = Disconnected
                DeviceConnectionStatus GetDeviceConnectionStatus()
                {
                    if (connectionStatus == ConnectionStatus.Connected)
                    {
                        return new DeviceConnectionStatus(connectionStatus, DateTime.UtcNow, null);
                    }
                    else
                    {
                        return client is IDeviceIdentity
                            ? null
                            : new DeviceConnectionStatus(connectionStatus, null, DateTime.UtcNow);
                    }
                }

                var connectedDevices = new Dictionary<string, DeviceConnectionStatus>
                {
                    [TwinManager.EncodeTwinKey(client.Id)] = GetDeviceConnectionStatus()
                };
                var edgeHubReportedProperties = new ReportedProperties(this.versionInfo, connectedDevices);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                IMessage reportedPropertiesMessage = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.twinManager.UpdateReportedPropertiesAsync(this.edgeHubIdentity.Id, reportedPropertiesMessage);
            }
            catch (Exception ex)
            {
                Events.ErrorUpdatingDeviceConnectionStatus(client.Id, ex);
                return Task.CompletedTask;
            }
        }

        Task ClearDeviceConnectionStatuses()
        {
            try
            {
                var edgeHubReportedProperties = new ReportedProperties(this.versionInfo, null);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                IMessage reportedPropertiesMessage = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.twinManager.UpdateReportedPropertiesAsync(this.edgeHubIdentity.Id, reportedPropertiesMessage);
            }
            catch (Exception ex)
            {
                Events.ErrorClearingDeviceConnectionStatuses(ex);
                return Task.CompletedTask;
            }
        }

        internal class LastDesiredStatus
        {
            public LastDesiredStatus(int code, string description)
            {
                this.Code = code;
                this.Description = description;
            }

            [JsonProperty(PropertyName = "code")]
            public int Code { get; set; }

            [JsonProperty(PropertyName = "description")]
            public string Description { get; set; }
        }

        internal class ReportedProperties
        {
            const string CurrentSchemaVersion = "1.0";

            static readonly Dictionary<string, DeviceConnectionStatus> EmptyConnectionStatusesDictionary = new Dictionary<string, DeviceConnectionStatus>();

            // When reporting last desired version/status, send empty map of clients so that the patch doesn't touch the
            // existing values. If we send a null, it will clear out the existing clients.
            public ReportedProperties(VersionInfo versionInfo, long lastDesiredVersion, LastDesiredStatus lastDesiredStatus)
                : this(versionInfo, CurrentSchemaVersion, lastDesiredVersion, lastDesiredStatus, EmptyConnectionStatusesDictionary)
            {
            }

            public ReportedProperties(VersionInfo versionInfo, IDictionary<string, DeviceConnectionStatus> clients)
                : this(versionInfo, CurrentSchemaVersion, null, null, clients)
            {
            }

            [JsonConstructor]
            public ReportedProperties(
                VersionInfo versionInfo,
                string schemaVersion,
                long? lastDesiredVersion,
                LastDesiredStatus lastDesiredStatus,
                IDictionary<string, DeviceConnectionStatus> clients)
            {
                this.SchemaVersion = schemaVersion;
                this.LastDesiredVersion = lastDesiredVersion;
                this.LastDesiredStatus = lastDesiredStatus;
                this.Clients = clients;
                this.VersionInfo = versionInfo ?? VersionInfo.Empty;
            }

            [JsonProperty(PropertyName = "schemaVersion")]
            public string SchemaVersion { get; }

            [JsonProperty(PropertyName = "lastDesiredVersion", NullValueHandling = NullValueHandling.Ignore)]
            public long? LastDesiredVersion { get; }

            [JsonProperty(PropertyName = "lastDesiredStatus", NullValueHandling = NullValueHandling.Ignore)]
            public LastDesiredStatus LastDesiredStatus { get; }

            [JsonProperty(PropertyName = "clients")]
            public IDictionary<string, DeviceConnectionStatus> Clients { get; }

            [JsonProperty(PropertyName = "version")]
            public VersionInfo VersionInfo { get; }
        }

        /// <summary>
        /// The Edge hub device proxy, that receives communication for the EdgeHub from the cloud.
        /// Currently only receives DesiredProperties updates.
        /// </summary>
        class EdgeHubDeviceProxy : IDeviceProxy
        {
            readonly EdgeHubConnection edgeHubConnection;

            public EdgeHubDeviceProxy(EdgeHubConnection edgeHubConnection)
            {
                this.edgeHubConnection = edgeHubConnection;
            }

            public bool IsActive => true;

            public bool IsDirectClient => true;

            public IIdentity Identity => this.edgeHubConnection.edgeHubIdentity;

            public Task CloseAsync(Exception ex) => Task.CompletedTask;

            public Task OnDesiredPropertyUpdates(IMessage desiredProperties) => this.edgeHubConnection.HandleDesiredPropertiesUpdate(desiredProperties);

            public Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request) =>
                this.edgeHubConnection.HandleMethodInvocation(request);

            public Task SendC2DMessageAsync(IMessage message)
            {
                throw new NotImplementedException();
            }

            public Task SendMessageAsync(IMessage message, string input)
            {
                throw new NotImplementedException();
            }

            public void SetInactive()
            {
                throw new NotImplementedException();
            }

            public Task SendTwinUpdate(IMessage twin)
            {
                throw new NotImplementedException();
            }

            public Task<Option<IClientCredentials>> GetUpdatedIdentity() => throw new NotImplementedException();
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.EdgeHubConnection;
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubConnection>();

            enum EventIds
            {
                Initialized = IdStart,
                ErrorHandlingDeviceDisconnectedEvent,
                ErrorHandlingDeviceConnectedEvent,
                ErrorUpdatingDeviceConnectionStatus,
                ErrorClearingDeviceConnectionStatuses,
                UpdatingDeviceConnectionStatus,
                ErrorParsingMethodRequest,
                ErrorRefreshingServiceIdentities,
                RefreshedServiceIdentities,
                InvalidMethodRequest,
                SkipUpdatingEdgeHubIdentity,
                MethodRequestReceived
            }

            public static void ErrorParsingMethodRequest(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorParsingMethodRequest, ex, Invariant($"Error parsing refresh service identities request"));
            }

            public static void ErrorRefreshingServiceIdentities(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorRefreshingServiceIdentities, ex, Invariant($"Error refreshing service identities"));
            }

            public static void RefreshedServiceIdentities(IEnumerable<string> refreshRequestDeviceIds)
            {
                Log.LogInformation((int)EventIds.RefreshedServiceIdentities, Invariant($"Refreshed {refreshRequestDeviceIds.Count()} device scope identities on demand"));
            }

            public static void RefreshingServiceIdentities(IEnumerable<string> refreshRequestDeviceIds)
            {
                Log.LogDebug((int)EventIds.RefreshedServiceIdentities, Invariant($"Refreshing {refreshRequestDeviceIds.Count()} device scope identities"));
            }

            public static void MethodRequestReceived(string methodName)
            {
                Log.LogDebug((int)EventIds.MethodRequestReceived, Invariant($"Received method request {methodName}"));
            }

            public static void InvalidMethodRequest(string requestName)
            {
                Log.LogWarning((int)EventIds.InvalidMethodRequest, Invariant($"Received request for unsupported method {requestName}"));
            }

            public static void SkipUpdatingEdgeHubIdentity(string id, ConnectionStatus connectionStatus)
            {
                Log.LogDebug((int)EventIds.SkipUpdatingEdgeHubIdentity, Invariant($"Skipped updating connection status change to {connectionStatus} for {id}"));
            }

            internal static void Initialized(IIdentity edgeHubIdentity)
            {
                Log.LogDebug((int)EventIds.Initialized, Invariant($"Initialized connection for {edgeHubIdentity.Id}"));
            }

            internal static void ErrorHandlingDeviceConnectedEvent(IIdentity device, Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorHandlingDeviceConnectedEvent,
                    ex,
                    Invariant($"Error handling device connected event for device {device?.Id ?? string.Empty}"));
            }

            internal static void ErrorHandlingDeviceDisconnectedEvent(IIdentity device, Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorHandlingDeviceDisconnectedEvent,
                    ex,
                    Invariant($"Error handling device disconnected event for device {device?.Id ?? string.Empty}"));
            }

            internal static void ErrorUpdatingDeviceConnectionStatus(string deviceId, Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorUpdatingDeviceConnectionStatus,
                    ex,
                    Invariant($"Error updating device connection status for device {deviceId ?? string.Empty}"));
            }

            internal static void ErrorClearingDeviceConnectionStatuses(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorClearingDeviceConnectionStatuses,
                    ex,
                    Invariant($"Error clearing device connection statuses"));
            }

            internal static void UpdatingDeviceConnectionStatus(string deviceId, ConnectionStatus connectionStatus)
            {
                Log.LogDebug((int)EventIds.UpdatingDeviceConnectionStatus, Invariant($"Updating device {deviceId} connection status to {connectionStatus}"));
            }
        }

        class RefreshRequest
        {
            [JsonConstructor]
            public RefreshRequest(IEnumerable<string> deviceIds)
            {
                this.DeviceIds = deviceIds;
            }

            [JsonProperty("deviceIds")]
            public IEnumerable<string> DeviceIds { get; }
        }
    }
}
