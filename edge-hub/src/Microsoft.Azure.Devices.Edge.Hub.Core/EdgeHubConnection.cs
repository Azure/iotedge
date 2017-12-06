// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static System.FormattableString;

    /// <summary>
    /// EdgeHubConnection is responsible for connecting to the cloud as the Edge hub and getting/upating the edge hub twin.
    /// </summary>
    public class EdgeHubConnection : IConfigSource
    {
        Func<EdgeHubConfig, Task> configUpdateCallback;
        Option<TwinCollection> lastDesiredProperties = Option.None<TwinCollection>();
        readonly IIdentity edgeHubIdentity;
        readonly ITwinManager twinManager;
        readonly IMessageConverter<TwinCollection> twinCollectionMessageConverter;
        readonly IMessageConverter<Twin> twinMessageConverter;
        readonly RouteFactory routeFactory;
        readonly AsyncLock edgeHubConfigLock = new AsyncLock();

        EdgeHubConnection(IIdentity edgeHubIdentity,
            ITwinManager twinManager,
            RouteFactory routeFactory,
            IMessageConverter<TwinCollection> twinCollectionMessageConverter,
            IMessageConverter<Twin> twinMessageConverter)
        {
            this.edgeHubIdentity = edgeHubIdentity;
            this.twinManager = twinManager;
            this.twinCollectionMessageConverter = twinCollectionMessageConverter;
            this.twinMessageConverter = twinMessageConverter;
            this.routeFactory = routeFactory;
        }

        public static async Task<EdgeHubConnection> Create(IIdentity edgeHubIdentity,
            ITwinManager twinManager,
            IConnectionManager connectionManager,
            RouteFactory routeFactory,
            IMessageConverter<TwinCollection> twinCollectionMessageConverter,
            IMessageConverter<Twin> twinMessageConverter)
        {
            Preconditions.CheckNotNull(edgeHubIdentity, nameof(edgeHubIdentity));
            Preconditions.CheckNotNull(twinManager, nameof(twinManager));
            Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(twinCollectionMessageConverter, nameof(twinCollectionMessageConverter));
            Preconditions.CheckNotNull(twinMessageConverter, nameof(twinMessageConverter));
            Preconditions.CheckNotNull(routeFactory, nameof(routeFactory));

            Try<ICloudProxy> cloudProxyTry = await connectionManager.GetOrCreateCloudConnectionAsync(edgeHubIdentity);
            if (!cloudProxyTry.Success)
            {
                throw new EdgeHubConnectionException("Edge hub is unable to connect to IoT Hub", cloudProxyTry.Exception);
            }

            ICloudProxy cloudProxy = cloudProxyTry.Value;
            var edgeHubConnection = new EdgeHubConnection(edgeHubIdentity, twinManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter);
            cloudProxy.BindCloudListener(new CloudListener(edgeHubConnection));

            IDeviceProxy deviceProxy = new EdgeHubDeviceProxy(edgeHubConnection);
            await connectionManager.AddDeviceConnection(edgeHubIdentity, deviceProxy);

            await cloudProxy.SetupDesiredPropertyUpdatesAsync();

            // Clear out all the reported devices.
            await edgeHubConnection.ClearDeviceConnectionStatuses();

            connectionManager.DeviceConnected += edgeHubConnection.DeviceConnected;
            connectionManager.DeviceDisconnected += edgeHubConnection.DeviceDisconnected;
            Events.Initialized(edgeHubIdentity);
            return edgeHubConnection;
        }

        public async Task<Option<EdgeHubConfig>> GetConfig()
        {
            using (await this.edgeHubConfigLock.LockAsync())
            {
                return await this.GetConfigInternal();
            }
        }

        // This method updates local state and should be called only after acquiring edgeHubConfigLock
        async Task<Option<EdgeHubConfig>> GetConfigInternal()
        {
            Option<EdgeHubConfig> edgeHubConfig;
            try
            {
                IMessage message = await this.twinManager.GetTwinAsync(this.edgeHubIdentity.Id);
                Twin twin = this.twinMessageConverter.FromMessage(message);
                this.lastDesiredProperties = Option.Some(twin.Properties.Desired);
                try
                {
                    var desiredProperties = JsonConvert.DeserializeObject<DesiredProperties>(twin.Properties.Desired.ToJson());
                    edgeHubConfig = Option.Some(this.GetEdgeHubConfig(desiredProperties));
                    await this.UpdateReportedProperties(twin.Properties.Desired.Version, new LastDesiredStatus(200, string.Empty));
                    Events.GetConfigSuccess();
                }
                catch (Exception ex)
                {
                    await this.UpdateReportedProperties(twin.Properties.Desired.Version, new LastDesiredStatus(400, $"Error while parsing desired properties - {ex.Message}"));
                    throw;
                }
            }
            catch (Exception ex)
            {
                edgeHubConfig = Option.None<EdgeHubConfig>();
                Events.ErrorGettingEdgeHubConfig(ex);
            }

            return edgeHubConfig;
        }

        EdgeHubConfig GetEdgeHubConfig(DesiredProperties desiredProperties)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));

            if (string.IsNullOrWhiteSpace(desiredProperties.SchemaVersion) ||
                !desiredProperties.SchemaVersion.Equals(Constants.ConfigSchemaVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Desired properties schema version {desiredProperties.SchemaVersion} is different from the expected schema version {Constants.ConfigSchemaVersion}");
            }

            var routes = new Dictionary<string, Route>();
            if (desiredProperties.Routes != null)
            {
                foreach (KeyValuePair<string, string> inputRoute in desiredProperties.Routes)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(inputRoute.Value))
                        {
                            Route route = this.routeFactory.Create(inputRoute.Value);
                            routes[inputRoute.Key] = route;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error parsing route {inputRoute.Key} - {ex.Message}", ex);
                    }
                }
            }

            return new EdgeHubConfig(desiredProperties.SchemaVersion, routes, desiredProperties.StoreAndForwardConfiguration);
        }

        /// <summary>
        /// If called multiple times, this will currently overwrite the existing callback
        /// </summary>
        public void SetConfigUpdatedCallback(Func<EdgeHubConfig, Task> callback) =>
            this.configUpdateCallback = Preconditions.CheckNotNull(callback, nameof(callback));

        async Task HandleDesiredPropertiesUpdate(IMessage desiredPropertiesUpdate)
        {
            try
            {
                TwinCollection twinCollection = this.twinCollectionMessageConverter.FromMessage(desiredPropertiesUpdate);
                using (await this.edgeHubConfigLock.LockAsync())
                {
                    Option<EdgeHubConfig> edgeHubConfig = await this.lastDesiredProperties
                        .Map(e => this.PatchDesiredProperties(e, twinCollection))
                        .GetOrElse(() => this.GetConfigInternal());

                    await edgeHubConfig.Map(async config =>
                    {
                        if (this.configUpdateCallback != null)
                        {
                            await this.configUpdateCallback(config);
                        }
                    })
                    .GetOrElse(Task.CompletedTask);
                }
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingDesiredPropertiesUpdate(ex);
            }
        }

        // This method updates local state and should be called only after acquiring edgeHubConfigLock
        async Task<Option<EdgeHubConfig>> PatchDesiredProperties(TwinCollection baseline, TwinCollection patch)
        {
            LastDesiredStatus lastDesiredStatus;
            Option<EdgeHubConfig> edgeHubConfig;
            try
            {
                string desiredPropertiesJson = JsonEx.Merge(baseline, patch, true);
                this.lastDesiredProperties = Option.Some(new TwinCollection(desiredPropertiesJson));
                var desiredPropertiesPatch = JsonConvert.DeserializeObject<DesiredProperties>(desiredPropertiesJson);
                edgeHubConfig = Option.Some(this.GetEdgeHubConfig(desiredPropertiesPatch));
                lastDesiredStatus = new LastDesiredStatus(200, string.Empty);
                Events.PatchConfigSuccess();
            }

            catch (Exception ex)
            {
                lastDesiredStatus = new LastDesiredStatus(400, $"Error while parsing desired properties - {ex.Message}");
                edgeHubConfig = Option.None<EdgeHubConfig>();
                Events.ErrorPatchingDesiredProperties(ex);
            }
            await this.UpdateReportedProperties(patch.Version, lastDesiredStatus);
            return edgeHubConfig;
        }

        Task UpdateReportedProperties(long desiredVersion, LastDesiredStatus desiredStatus)
        {
            try
            {
                var edgeHubReportedProperties = new ReportedProperties(desiredVersion, desiredStatus);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                IMessage message = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.twinManager.UpdateReportedPropertiesAsync(this.edgeHubIdentity.Id, message);
            }
            catch (Exception ex)
            {
                Events.ErrorUpdatingLastDesiredStatus(ex);
                return Task.CompletedTask;
            }
        }

        async void DeviceDisconnected(object sender, IIdentity device)
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

        async void DeviceConnected(object sender, IIdentity device)
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

        Task UpdateDeviceConnectionStatus(IIdentity client, ConnectionStatus connectionStatus)
        {
            try
            {
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
                    [client.Id] = GetDeviceConnectionStatus()
                };
                var edgeHubReportedProperties = new ReportedProperties(connectedDevices);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                IMessage message = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.twinManager.UpdateReportedPropertiesAsync(this.edgeHubIdentity.Id, message);
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
                var edgeHubReportedProperties = new ReportedProperties(null);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                IMessage message = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.twinManager.UpdateReportedPropertiesAsync(this.edgeHubIdentity.Id, message);
            }
            catch (Exception ex)
            {
                Events.ErrorClearingDeviceConnectionStatuses(ex);
                return Task.CompletedTask;
            }
        }

        class DesiredProperties
        {
            [JsonConstructor]
            public DesiredProperties(string schemaVersion, IDictionary<string, string> routes, StoreAndForwardConfiguration storeAndForwardConfiguration)
            {
                this.SchemaVersion = schemaVersion;
                this.Routes = routes;
                this.StoreAndForwardConfiguration = storeAndForwardConfiguration;
            }

            public string SchemaVersion { get; }

            public IDictionary<string, string> Routes { get; }

            public StoreAndForwardConfiguration StoreAndForwardConfiguration { get; }
        }

        internal class ReportedProperties
        {
            const string CurrentSchemaVersion = "1.0";

            static Dictionary<string, DeviceConnectionStatus> EmptyConnectionStatusesDictionary = new Dictionary<string, DeviceConnectionStatus>();

            // When reporting last desired version/status, send empty map of clients so that the patch doesn't touch the 
            // existing values. If we send a null, it will clear out the existing clients.
            public ReportedProperties(long lastDesiredVersion, LastDesiredStatus lastDesiredStatus)
                : this(CurrentSchemaVersion, lastDesiredVersion, lastDesiredStatus, EmptyConnectionStatusesDictionary) { }

            public ReportedProperties(IDictionary<string, DeviceConnectionStatus> clients)
                : this(CurrentSchemaVersion, null, null, clients) { }

            [JsonConstructor]
            public ReportedProperties(string schemaVersion, long? lastDesiredVersion, LastDesiredStatus lastDesiredStatus, IDictionary<string, DeviceConnectionStatus> clients)
            {
                this.SchemaVersion = schemaVersion;
                this.LastDesiredVersion = lastDesiredVersion;
                this.LastDesiredStatus = lastDesiredStatus;
                this.Clients = clients;
            }

            [JsonProperty(PropertyName = "schemaVersion")]
            public string SchemaVersion { get; }

            [JsonProperty(PropertyName = "lastDesiredVersion", NullValueHandling = NullValueHandling.Ignore)]
            public long? LastDesiredVersion { get; }

            [JsonProperty(PropertyName = "lastDesiredStatus", NullValueHandling = NullValueHandling.Ignore)]
            public LastDesiredStatus LastDesiredStatus { get; }

            [JsonProperty(PropertyName = "clients")]
            public IDictionary<string, DeviceConnectionStatus> Clients { get; }
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

            public IIdentity Identity => this.edgeHubConnection.edgeHubIdentity;

            public Task CloseAsync(Exception ex) => Task.CompletedTask;

            public Task OnDesiredPropertyUpdates(IMessage desiredProperties) => this.edgeHubConnection.HandleDesiredPropertiesUpdate(desiredProperties);

            public Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request)
            {
                throw new NotImplementedException();
            }

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
        }

        /// <summary>
        /// Cloud listener that listens to updates from the cloud for the edge hub module
        /// Currently only listens for DesiredProperties updates
        /// </summary>
        class CloudListener : ICloudListener
        {
            readonly EdgeHubConnection edgeHubConnection;

            public CloudListener(EdgeHubConnection edgeHubConnection)
            {
                this.edgeHubConnection = edgeHubConnection;
            }

            public Task OnDesiredPropertyUpdates(IMessage desiredProperties) => this.edgeHubConnection.HandleDesiredPropertiesUpdate(desiredProperties);

            public Task<DirectMethodResponse> CallMethodAsync(DirectMethodRequest request)
            {
                throw new NotImplementedException();
            }

            public Task ProcessMessageAsync(IMessage message)
            {
                throw new NotImplementedException();
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubConnection>();
            const int IdStart = HubCoreEventIds.EdgeHubConnection;

            enum EventIds
            {
                Initialized = IdStart,
                ErrorUpdatingLastDesiredStatus,
                ErrorHandlingDesiredPropertiesUpdate,
                ErrorPatchingDesiredProperties,
                ErrorHandlingDeviceDisconnectedEvent,
                ErrorHandlingDeviceConnectedEvent,
                ErrorUpdatingDeviceConnectionStatus,
                GetConfigSuccess,
                PatchConfigSuccess,
                ErrorClearingDeviceConnectionStatuses,
                UpdatingDeviceConnectionStatus
            }

            internal static void Initialized(IIdentity edgeHubIdentity)
            {
                Log.LogDebug((int)EventIds.Initialized, Invariant($"Established IoT Hub connection for edge hub {edgeHubIdentity.Id}"));
            }

            internal static void ErrorUpdatingLastDesiredStatus(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorUpdatingLastDesiredStatus, ex,
                    Invariant($"Error updating last desired status for edge hub"));
            }

            internal static void ErrorHandlingDesiredPropertiesUpdate(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingDesiredPropertiesUpdate, ex,
                    Invariant($"Error handling desired properties update for edge hub"));
            }

            internal static void ErrorPatchingDesiredProperties(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorPatchingDesiredProperties, ex,
                    Invariant($"Error merging desired properties patch with existing desired properties for edge hub"));
            }

            internal static void ErrorHandlingDeviceConnectedEvent(IIdentity device, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingDeviceConnectedEvent, ex,
                    Invariant($"Error handling device connected event for device {device?.Id ?? string.Empty}"));
            }

            internal static void ErrorHandlingDeviceDisconnectedEvent(IIdentity device, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingDeviceDisconnectedEvent, ex,
                    Invariant($"Error handling device disconnected event for device {device?.Id ?? string.Empty}"));
            }

            internal static void ErrorUpdatingDeviceConnectionStatus(string deviceId, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorUpdatingDeviceConnectionStatus, ex,
                    Invariant($"Error updating device connection status for device {deviceId ?? string.Empty}"));
            }

            public static void ErrorGettingEdgeHubConfig(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorPatchingDesiredProperties, ex,
                    Invariant($"Error getting edge hub config from twin desired properties"));
            }

            internal static void GetConfigSuccess()
            {
                Log.LogInformation((int)EventIds.GetConfigSuccess, Invariant($"Obtained edge hub config from module twin"));
            }

            internal static void PatchConfigSuccess()
            {
                Log.LogInformation((int)EventIds.PatchConfigSuccess, Invariant($"Obtained edge hub config patch update from module twin"));
            }

            internal static void ErrorClearingDeviceConnectionStatuses(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorClearingDeviceConnectionStatuses, ex,
                    Invariant($"Error clearing device connection statuses"));
            }

            internal static void UpdatingDeviceConnectionStatus(string deviceId, ConnectionStatus connectionStatus)
            {
                Log.LogDebug((int)EventIds.UpdatingDeviceConnectionStatus, Invariant($"Updating device {deviceId} connection status to {connectionStatus}"));
            }
        }
    }
}
