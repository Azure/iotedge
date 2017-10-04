// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
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
        EdgeHubConfig edgeHubConfig;
        long lastDesiredVersion = -1;
        readonly ICloudProxy cloudProxy;
        readonly IMessageConverter<TwinCollection> twinCollectionMessageConverter;
        readonly IMessageConverter<Twin> twinMessageConverter;
        readonly RouteFactory routeFactory;
        readonly AsyncLock edgeHubConfigLock = new AsyncLock();

        EdgeHubConnection(ICloudProxy cloudProxy,
            RouteFactory routeFactory,
            IMessageConverter<TwinCollection> twinCollectionMessageConverter,
            IMessageConverter<Twin> twinMessageConverter)
        {
            this.cloudProxy = cloudProxy;
            this.twinCollectionMessageConverter = twinCollectionMessageConverter;
            this.twinMessageConverter = twinMessageConverter;
            this.routeFactory = routeFactory;
        }

        public static async Task<EdgeHubConnection> Create(IIdentity edgeHubIdentity,
            IConnectionManager connectionManager,
            RouteFactory routeFactory,
            IMessageConverter<TwinCollection> twinCollectionMessageConverter,
            IMessageConverter<Twin> twinMessageConverter)
        {
            Preconditions.CheckNotNull(edgeHubIdentity, nameof(edgeHubIdentity));
            Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(twinCollectionMessageConverter, nameof(twinCollectionMessageConverter));
            Preconditions.CheckNotNull(twinMessageConverter, nameof(twinMessageConverter));
            Preconditions.CheckNotNull(routeFactory, nameof(routeFactory));

            Try<ICloudProxy> cloudProxyTry = await connectionManager.GetOrCreateCloudConnectionAsync(edgeHubIdentity);
            if (!cloudProxyTry.Success)
            {
                throw new EdgeHubConnectionException("Edge Hub is unable to connect to IoTHub", cloudProxyTry.Exception);
            }

            ICloudProxy cloudProxy = cloudProxyTry.Value;
            var edgeHubConnection = new EdgeHubConnection(cloudProxy, routeFactory, twinCollectionMessageConverter, twinMessageConverter);            
            cloudProxy.BindCloudListener(new CloudListener(edgeHubConnection));            
            await cloudProxy.SetupDesiredPropertyUpdatesAsync();

            // Clear out all the reported devices.
            await edgeHubConnection.ClearDeviceConnectionStatuses();

            connectionManager.DeviceConnected += edgeHubConnection.DeviceConnected;
            connectionManager.DeviceDisconnected += edgeHubConnection.DeviceDisconnected;
            Events.Initialized(edgeHubIdentity);
            return edgeHubConnection;
        }

        public async Task<EdgeHubConfig> GetConfig()
        {
            using (await this.edgeHubConfigLock.LockAsync())
            {
                IMessage message = await this.cloudProxy.GetTwinAsync();
                Twin twin = this.twinMessageConverter.FromMessage(message);
                long desiredVersion = twin.Properties.Desired.Version;
                try
                {
                    var desiredProperties = JsonConvert.DeserializeObject<DesiredProperties>(twin.Properties.Desired.ToJson());
                    this.edgeHubConfig = this.GetEdgeHubConfig(desiredProperties, true);
                    this.lastDesiredVersion = desiredVersion;
                    await this.UpdateReportedProperties(twin.Properties.Desired.Version, new LastDesiredStatus(200, string.Empty));
                    Events.GetConfigSuccess();
                    return this.edgeHubConfig;
                }
                catch (Exception ex)
                {
                    await this.UpdateReportedProperties(desiredVersion, new LastDesiredStatus(400, $"Error while parsing desired properties - {ex.Message}"));
                    Events.ErrorGettingEdgeHubConfig(ex);
                    return new EdgeHubConfig(Constants.ConfigSchemaVersion, null, null);
                }
            }
        }

        EdgeHubConfig GetEdgeHubConfig(DesiredProperties desiredProperties, bool requireSchemaVersion)
        {
            Preconditions.CheckNotNull(desiredProperties, nameof(desiredProperties));

            if (requireSchemaVersion && (string.IsNullOrWhiteSpace(desiredProperties.SchemaVersion) ||
                !desiredProperties.SchemaVersion.Equals(Constants.ConfigSchemaVersion, StringComparison.OrdinalIgnoreCase)))
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
                        Route route = this.routeFactory.Create(inputRoute.Value);
                        routes[inputRoute.Key] = route;
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
            TwinCollection twinCollection = this.twinCollectionMessageConverter.FromMessage(desiredPropertiesUpdate);
            long desiredVersion = twinCollection.Version;

            using (await this.edgeHubConfigLock.LockAsync())
            {
                // If the cached config is null, or if the desiredVersion is less than the 
                // last seen desired version, then do a GetConfig again (this will update the 
                // cached copy of the config)
                if (this.edgeHubConfig == null || desiredVersion != this.lastDesiredVersion + 1)
                {
                    await this.GetConfig();
                }
                else
                {
                    await this.PatchDesiredProperties(twinCollection);
                    Events.PatchConfigSuccess();
                }

                try
                {
                    if (this.configUpdateCallback != null)
                    {
                        await this.configUpdateCallback(this.edgeHubConfig);
                    }
                }
                catch (Exception ex)
                {
                    Events.ErrorHandlingDesiredPropertiesUpdate(ex);
                }
            }
        }

        async Task PatchDesiredProperties(TwinCollection twinCollection)
        {
            LastDesiredStatus lastDesiredStatus;
            try
            {
                var desiredPropertiesPatch = JsonConvert.DeserializeObject<DesiredProperties>(twinCollection.ToJson());
                EdgeHubConfig edgeHubConfigPatch = this.GetEdgeHubConfig(desiredPropertiesPatch, false);
                this.edgeHubConfig.ApplyDiff(edgeHubConfigPatch);
                this.lastDesiredVersion = twinCollection.Version;
                lastDesiredStatus = new LastDesiredStatus(200, string.Empty);
            }

            catch (Exception ex)
            {
                lastDesiredStatus = new LastDesiredStatus(400, $"Error while parsing desired properties - {ex.Message}");
                Events.ErrorPatchingDesiredProperties(ex);
            }
            await this.UpdateReportedProperties(twinCollection.Version, lastDesiredStatus);
        }

        Task UpdateReportedProperties(long desiredVersion, LastDesiredStatus desiredStatus)
        {
            try
            {
                var edgeHubReportedProperties = new ReportedProperties(desiredVersion, desiredStatus);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                IMessage message = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.cloudProxy.UpdateReportedPropertiesAsync(message);
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
                await this.UpdateDeviceConnectionStatus(device.Id, new DeviceConnectionStatus(ConnectionStatus.Disconnected, null, DateTime.UtcNow));
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
                await this.UpdateDeviceConnectionStatus(device.Id, new DeviceConnectionStatus(ConnectionStatus.Connected, DateTime.UtcNow, null));
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingDeviceConnectedEvent(device, ex);
            }
        }

        Task UpdateDeviceConnectionStatus(string deviceId, DeviceConnectionStatus deviceConnectionStatus)
        {
            try
            {
                var connectedDevices = new Dictionary<string, DeviceConnectionStatus>
                {
                    [deviceId] = deviceConnectionStatus
                };
                var edgeHubReportedProperties = new ReportedProperties(connectedDevices);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                IMessage message = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.cloudProxy.UpdateReportedPropertiesAsync(message);
            }
            catch (Exception ex)
            {
                Events.ErrorUpdatingDeviceConnectionStatus(deviceId, ex);
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
                return this.cloudProxy.UpdateReportedPropertiesAsync(message);
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
            static Dictionary<string, DeviceConnectionStatus> EmptyConnectionStatusesDictionary = new Dictionary<string, DeviceConnectionStatus>();

            // When reporting last desired version/status, send empty map of clients so that the patch doesn't touch the 
            // existing values. If we send a null, it will clear out the existing clients.
            public ReportedProperties(long lastDesiredVersion, LastDesiredStatus lastDesiredStatus)
                : this(lastDesiredVersion, lastDesiredStatus, EmptyConnectionStatusesDictionary) { }

            public ReportedProperties(IDictionary<string, DeviceConnectionStatus> clients)
                : this(null, null, clients) { }

            [JsonConstructor]
            public ReportedProperties(long? lastDesiredVersion, LastDesiredStatus lastDesiredStatus, IDictionary<string, DeviceConnectionStatus> clients)
            {
                this.LastDesiredVersion = lastDesiredVersion;
                this.LastDesiredStatus = lastDesiredStatus;
                this.Clients = clients;
            }

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
                ErrorClearingDeviceConnectionStatuses
            }

            internal static void Initialized(IIdentity edgeHubIdentity)
            {
                Log.LogDebug((int)EventIds.Initialized, Invariant($"Established connection for Edge Hub {edgeHubIdentity.Id}"));
            }

            internal static void ErrorUpdatingLastDesiredStatus(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorUpdatingLastDesiredStatus, ex,
                    Invariant($"Error updating last desired status for Edge Hub"));
            }

            internal static void ErrorHandlingDesiredPropertiesUpdate(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingDesiredPropertiesUpdate, ex,
                    Invariant($"Error handling desired properties update for Edge Hub"));
            }

            internal static void ErrorPatchingDesiredProperties(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorPatchingDesiredProperties, ex,
                    Invariant($"Error merging desired properties patch with existing desired properties for Edge Hub"));
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
                Log.LogError((int)EventIds.ErrorPatchingDesiredProperties, ex,
                    Invariant($"Error getting edge hub config from twin desired properties"));
            }

            internal static void GetConfigSuccess()
            {
                Log.LogInformation((int)EventIds.GetConfigSuccess, Invariant($"Obtained Edge Hub config from module twin"));
            }

            internal static void PatchConfigSuccess()
            {
                Log.LogInformation((int)EventIds.PatchConfigSuccess, Invariant($"Obtained Edge Hub config patch update from module twin"));
            }

            internal static void ErrorClearingDeviceConnectionStatuses(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorClearingDeviceConnectionStatuses, ex,
                    Invariant($"Error clearing device connection statuses"));
            }
        }
    }
}
