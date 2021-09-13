// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static System.FormattableString;
    using static Microsoft.Azure.Devices.Edge.Hub.Core.EdgeHubConnection;

    public class TwinConfigSource : IConfigSource
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();

        readonly AsyncLock configLock = new AsyncLock();
        readonly ITwinManager twinManager;
        readonly string id;
        readonly RouteFactory routeFactory;
        readonly Core.IMessageConverter<TwinCollection> twinCollectionMessageConverter;
        readonly Core.IMessageConverter<Twin> twinMessageConverter;
        readonly VersionInfo versionInfo;
        readonly EdgeHubConnection edgeHubConnection;
        Option<TwinCollection> lastDesiredProperties;
        Func<EdgeHubConfig, Task> configUpdateCallback;

        public TwinConfigSource(EdgeHubConnection edgeHubConnection, string id, VersionInfo versionInfo, ITwinManager twinManager, Core.IMessageConverter<Twin> messageConverter, Core.IMessageConverter<TwinCollection> twinCollectionMessageConverter, RouteFactory routeFactory)
        {
            this.edgeHubConnection = Preconditions.CheckNotNull(edgeHubConnection, nameof(edgeHubConnection));
            this.id = Preconditions.CheckNotNull(id, nameof(id));
            this.twinManager = Preconditions.CheckNotNull(twinManager, nameof(twinManager));
            this.twinMessageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.twinCollectionMessageConverter = twinCollectionMessageConverter;
            this.routeFactory = Preconditions.CheckNotNull(routeFactory, nameof(routeFactory));
            this.versionInfo = versionInfo ?? VersionInfo.Empty;
        }

        public async Task<Option<EdgeHubConfig>> GetCachedConfig()
        {
            try
            {
                Option<Core.IMessage> twinMessage = await this.twinManager.GetCachedTwinAsync(this.id);

                var config = twinMessage.FlatMap(
                    (message) =>
                    {
                        Twin twin = this.twinMessageConverter.FromMessage(message);
                        if (twin.Properties.Desired.Count > 0)
                        {
                            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(twin.Properties.Desired.ToJson());
                            return Option.Some(EdgeHubConfigParser.GetEdgeHubConfig(desiredProperties, this.routeFactory));
                        }
                        else
                        {
                            return Option.None<EdgeHubConfig>();
                        }
                    });

                return config;
            }
            catch (Exception e)
            {
                Events.FailedGettingCachedConfig(e);
                return Option.None<EdgeHubConfig>();
            }
        }

        public async Task<Option<EdgeHubConfig>> GetConfig()
        {
            using (await this.configLock.LockAsync())
            {
                return await this.GetConfigInternal();
            }
        }

        public void SetConfigUpdatedCallback(Func<EdgeHubConfig, Task> callback)
        {
            this.configUpdateCallback = callback;
            this.edgeHubConnection.SetDesiredPropertiesUpdateCallback((message) => this.HandleDesiredPropertiesUpdate(message));
        }

        // This method updates local state and should be called only after acquiring edgeHubConfigLock
        async Task<Option<EdgeHubConfig>> GetConfigInternal()
        {
            Option<EdgeHubConfig> edgeHubConfig;
            try
            {
                Core.IMessage message = await this.twinManager.GetTwinAsync(this.id);
                Twin twin = this.twinMessageConverter.FromMessage(message);
                try
                {
                    var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(twin.Properties.Desired.ToJson());
                    edgeHubConfig = Option.Some(EdgeHubConfigParser.GetEdgeHubConfig(desiredProperties, this.routeFactory));
                    
                    this.lastDesiredProperties = Option.Some(twin.Properties.Desired);
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

        async Task HandleDesiredPropertiesUpdate(Core.IMessage desiredPropertiesUpdate)
        {
            try
            {
                TwinCollection twinCollection = this.twinCollectionMessageConverter.FromMessage(desiredPropertiesUpdate);
                using (await this.configLock.LockAsync())
                {
                    Option<EdgeHubConfig> edgeHubConfig = await this.lastDesiredProperties
                        .Map(e => this.PatchDesiredProperties(e, twinCollection))
                        .GetOrElse(() => this.GetConfigInternal());

                    await edgeHubConfig.ForEachAsync(
                        async config =>
                        {
                            if (this.configUpdateCallback != null)
                            {
                                await this.configUpdateCallback(config);
                            }
                        });
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
                var desiredPropertiesPatch = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(desiredPropertiesJson);
                edgeHubConfig = Option.Some(EdgeHubConfigParser.GetEdgeHubConfig(desiredPropertiesPatch, this.routeFactory));

                this.lastDesiredProperties = Option.Some(new TwinCollection(desiredPropertiesJson));
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
                var edgeHubReportedProperties = new ReportedProperties(this.versionInfo, desiredVersion, desiredStatus);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                Core.IMessage reportedPropertiesMessage = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.twinManager.UpdateReportedPropertiesAsync(this.id, reportedPropertiesMessage);
            }
            catch (Exception ex)
            {
                Events.ErrorUpdatingLastDesiredStatus(ex);
                return Task.CompletedTask;
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.TwinConfigSource;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();

            enum EventIds
            {
                Initialized = IdStart,
                GetConfigSuccess,
                ErrorPatchingDesiredProperties,
                ErrorUpdatingLastDesiredStatus,
                ErrorHandlingDesiredPropertiesUpdate,
                PatchConfigSuccess,
                ErrorGettingCachedConfig
            }

            internal static void ErrorGettingEdgeHubConfig(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorPatchingDesiredProperties,
                    ex,
                    Invariant($"Error getting edge hub config from twin desired properties"));
            }

            internal static void ErrorUpdatingLastDesiredStatus(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorUpdatingLastDesiredStatus,
                    ex,
                    Invariant($"Error updating last desired status for edge hub"));
            }

            internal static void ErrorHandlingDesiredPropertiesUpdate(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorHandlingDesiredPropertiesUpdate,
                    ex,
                    Invariant($"Error handling desired properties update for edge hub"));
            }

            internal static void ErrorPatchingDesiredProperties(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorPatchingDesiredProperties,
                    ex,
                    Invariant($"Error merging desired properties patch with existing desired properties for edge hub"));
            }

            internal static void GetConfigSuccess()
            {
                Log.LogInformation((int)EventIds.GetConfigSuccess, Invariant($"Obtained edge hub config from module twin"));
            }

            internal static void PatchConfigSuccess()
            {
                Log.LogInformation((int)EventIds.PatchConfigSuccess, Invariant($"Obtained edge hub config patch update from module twin"));
            }

            internal static void FailedGettingCachedConfig(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorGettingCachedConfig,
                    ex,
                    Invariant($"Failed to get local config"));
            }
        }
    }
}
