// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage.Disk;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;

    public class ConfigUpdater
    {
        readonly Router router;
        readonly IMessageStore messageStore;
        readonly TimeSpan configUpdateFrequency;
        readonly IDiskSpaceChecker diskSpaceChecker;
        readonly AsyncLock updateLock = new AsyncLock();

        Option<PeriodicTask> configUpdater;
        Option<EdgeHubConfig> currentConfig;
        Option<IConfigSource> configProvider;

        public ConfigUpdater(Router router, IMessageStore messageStore, TimeSpan configUpdateFrequency, IDiskSpaceChecker diskSpaceChecker)
        {
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.messageStore = messageStore;
            this.configUpdateFrequency = configUpdateFrequency;
            this.diskSpaceChecker = diskSpaceChecker;
        }

        public void Init(IConfigSource configProvider)
        {
            Preconditions.CheckNotNull(configProvider, nameof(configProvider));
            try
            {
                configProvider.SetConfigUpdatedCallback(this.UpdateConfig);
                this.configProvider = Option.Some(configProvider);
                this.configUpdater = Option.Some(new PeriodicTask(this.PullConfig, this.configUpdateFrequency, TimeSpan.Zero, Events.Log, "Get EdgeHub config"));
                Events.Initialized();
            }
            catch (Exception ex)
            {
                Events.InitializingError(ex);
                throw;
            }
        }

        async Task PullConfig()
        {
            try
            {
                Option<EdgeHubConfig> edgeHubConfig = await this.configProvider
                    .Map(c => c.GetConfig())
                    .GetOrElse(Task.FromResult(Option.None<EdgeHubConfig>()));
                if (!edgeHubConfig.HasValue)
                {
                    Events.EmptyConfigReceived();
                }
                else
                {
                    using (await this.updateLock.LockAsync())
                    {
                        await edgeHubConfig.ForEachAsync(
                            async ehc =>
                            {
                                bool hasUpdates = this.currentConfig.Map(cc => !cc.Equals(ehc)).GetOrElse(true);
                                if (hasUpdates)
                                {
                                    await this.UpdateRoutes(ehc.Routes, this.currentConfig.HasValue);
                                    this.UpdateStoreAndForwardConfig(ehc.StoreAndForwardConfiguration);
                                    this.currentConfig = Option.Some(ehc);
                                }
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                Events.ErrorPullingConfig(ex);
            }
        }

        async Task UpdateConfig(EdgeHubConfig edgeHubConfig)
        {
            Preconditions.CheckNotNull(edgeHubConfig, nameof(edgeHubConfig));
            Events.UpdatingConfig();
            try
            {
                using (await this.updateLock.LockAsync())
                {
                    await this.UpdateRoutes(edgeHubConfig.Routes, true);
                    this.UpdateStoreAndForwardConfig(edgeHubConfig.StoreAndForwardConfiguration);
                }
            }
            catch (Exception ex)
            {
                Events.UpdateError(ex);
            }
        }

        async Task UpdateRoutes(IReadOnlyDictionary<string, RouteConfig> routes, bool replaceExisting)
        {
            if (routes != null)
            {
                ISet<Route> routeSet = new HashSet<Route>(routes.Select(r => r.Value.Route));
                if (replaceExisting)
                {
                    await this.router.ReplaceRoutes(routeSet);
                }
                else
                {
                    foreach (Route route in routeSet)
                    {
                        await this.router.SetRoute(route);
                    }
                }

                Events.RoutesUpdated(routes);
            }
        }

        void UpdateStoreAndForwardConfig(StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            if (storeAndForwardConfiguration != null)
            {
                this.messageStore?.SetTimeToLive(storeAndForwardConfiguration.TimeToLive);
                storeAndForwardConfiguration.MaxStorageSpaceBytes.ForEach(s => this.diskSpaceChecker?.SetMaxDiskUsageSize(s));
                Events.UpdatedStoreAndForwardConfiguration();
            }
        }

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<ConfigUpdater>();
            const int IdStart = HubCoreEventIds.ConfigUpdater;

            enum EventIds
            {
                Initialized = IdStart,
                InitializeError,
                UpdateError,
                UpdatingConfig,
                UpdatedRoutes,
                UpdatedStoreAndForwardConfig,
                EmptyConfig,
                ErrorPullingConfig
            }

            public static void ErrorPullingConfig(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorPullingConfig, ex, FormattableString.Invariant($"Error getting edge hub configuration."));
            }

            internal static void Initialized()
            {
                Log.LogInformation((int)EventIds.Initialized, FormattableString.Invariant($"Initialized edge hub configuration"));
            }

            internal static void InitializingError(Exception ex)
            {
                Log.LogError(
                    (int)EventIds.InitializeError,
                    ex,
                    FormattableString.Invariant($"Error initializing edge hub configuration"));
            }

            internal static void UpdateError(Exception ex)
            {
                Log.LogError(
                    (int)EventIds.UpdateError,
                    ex,
                    FormattableString.Invariant($"Error updating edge hub configuration"));
            }

            internal static void UpdatingConfig()
            {
                Log.LogInformation((int)EventIds.UpdatingConfig, "Updating edge hub configuration");
            }

            internal static void RoutesUpdated(IReadOnlyDictionary<string, RouteConfig> routes)
            {
                if (routes.Count > 0)
                {
                    Log.LogInformation((int)EventIds.UpdatedRoutes, $"Set the following {routes.Count} route(s) in edge hub");
                    foreach (KeyValuePair<string, RouteConfig> route in routes)
                    {
                        Log.LogInformation((int)EventIds.UpdatedRoutes, $"{route.Value.Name}: {route.Value.Value}");
                    }
                }
                else
                {
                    Log.LogInformation((int)EventIds.UpdatedRoutes, "No routes set in the edge hub");
                }
            }

            internal static void UpdatedStoreAndForwardConfiguration()
            {
                Log.LogInformation((int)EventIds.UpdatedStoreAndForwardConfig, "Updated the edge hub store and forward configuration");
            }

            internal static void EmptyConfigReceived()
            {
                Log.LogWarning((int)EventIds.EmptyConfig, FormattableString.Invariant($"Empty edge hub configuration received. Ignoring..."));
            }
        }
    }
}
