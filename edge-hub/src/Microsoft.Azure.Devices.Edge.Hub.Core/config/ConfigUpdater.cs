// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;

    public class ConfigUpdater
    {
        readonly Router router;
        readonly IMessageStore messageStore;
        readonly TimeSpan configUpdateFrequency;
        readonly IStorageSpaceChecker storageSpaceChecker;

        readonly AsyncLock updateLock = new AsyncLock();

        Option<PeriodicTask> configUpdater;
        Option<EdgeHubConfig> currentConfig;
        Option<IConfigSource> configProvider;

        public ConfigUpdater(Router router, IMessageStore messageStore, TimeSpan configUpdateFrequency, IStorageSpaceChecker storageSpaceChecker)
        {
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.messageStore = messageStore;
            this.configUpdateFrequency = configUpdateFrequency;
            this.storageSpaceChecker = Preconditions.CheckNotNull(storageSpaceChecker, nameof(storageSpaceChecker));
        }

        public async Task Init(IConfigSource configProvider)
        {
            Preconditions.CheckNotNull(configProvider, nameof(configProvider));
            try
            {
                configProvider.SetConfigUpdatedCallback(this.HandleUpdateConfig);
                this.configProvider = Option.Some(configProvider);

                // first try to update config with the cached config
                await this.PullConfig(c => c.GetCachedConfig());

                // Get the config and initialize the EdgeHub
                // but don't wait if it has a prefetched config
                Task pullTask = this.PullConfig(c => c.GetConfig());
                await this.currentConfig.Match(
                    (config) =>
                    {
                        Events.InitializedWithPrefetchedConfig();
                        return Task.FromResult(this.currentConfig);
                    },
                    async () =>
                    {
                        Events.GettingConfig();
                        await pullTask;

                        this.currentConfig.Expect<InvalidOperationException>(() => throw new InvalidOperationException(
                                        "Could not obtain twin neither from local store nor from cloud. " +
                                        "This happens when there is no upstream connection and this is the first EdgeHub startup, " +
                                        "or there is no persistent store to save a previous twin configuration. " +
                                        "EdgeHub cannot start without basic configuration stored in twin. Stopping now."));

                        return this.currentConfig;
                    });

                // Start a periodic task to pull the config.
                this.configUpdater = Option.Some(new PeriodicTask(() => this.PullConfig(c => c.GetConfig()), this.configUpdateFrequency, this.configUpdateFrequency, Events.Log, "Get EdgeHub config"));
                Events.Initialized();
            }
            catch (Exception ex)
            {
                Events.InitializingError(ex);
                throw;
            }
        }

        async Task PullConfig(Func<IConfigSource, Task<Option<EdgeHubConfig>>> configGetter)
        {
            try
            {
                Option<EdgeHubConfig> edgeHubConfig = await this.configProvider
                    .Map(c => configGetter(c))
                    .GetOrElse(Task.FromResult(Option.None<EdgeHubConfig>()));
                if (!edgeHubConfig.HasValue)
                {
                    Events.EmptyConfigReceived();
                }
                else
                {
                    await this.UpdateConfig(edgeHubConfig);
                }
            }
            catch (Exception ex)
            {
                Events.ErrorPullingConfig(ex);
            }
        }

        async Task UpdateConfig(Option<EdgeHubConfig> edgeHubConfig)
        {
            using (await this.updateLock.LockAsync())
            {
                await edgeHubConfig.ForEachAsync(
                    async ehc =>
                    {
                        bool hasUpdates = this.currentConfig.Map(cc => !cc.Equals(ehc)).GetOrElse(true);
                        Events.ConfigReceived(hasUpdates);
                        if (hasUpdates)
                        {
                            await this.UpdateRoutes(ehc.Routes, this.currentConfig.HasValue);
                            this.UpdateStoreAndForwardConfig(ehc.StoreAndForwardConfiguration);
                            this.currentConfig = Option.Some(ehc);
                        }
                    });
            }
        }

        async Task HandleUpdateConfig(EdgeHubConfig edgeHubConfig)
        {
            Preconditions.CheckNotNull(edgeHubConfig, nameof(edgeHubConfig));
            Events.UpdatingConfig();
            try
            {
                await this.UpdateConfig(Option.Some(edgeHubConfig));
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

                if (storeAndForwardConfiguration.StoreLimits.HasValue)
                {
                    storeAndForwardConfiguration.StoreLimits.ForEach(x => this.storageSpaceChecker.SetMaxSizeBytes(Option.Some(x.MaxSizeBytes)));
                }
                else
                {
                    this.storageSpaceChecker.SetMaxSizeBytes(Option.None<long>());
                }

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
                ErrorPullingConfig,
                ConfigReceived,
                GettingConfig,
                InitializedWithPrefechedConfig
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

            internal static void ConfigReceived(bool hasUpdates)
            {
                string hasUpdatesMessage = hasUpdates ? string.Empty : "no";
                Log.LogDebug((int)EventIds.ConfigReceived, $"Received edge hub configuration with {hasUpdatesMessage} updates");
            }

            internal static void GettingConfig()
            {
                Log.LogDebug((int)EventIds.GettingConfig, $"Getting configuration");
            }

            internal static void InitializedWithPrefetchedConfig()
            {
                Log.LogDebug((int)EventIds.InitializedWithPrefechedConfig, $"Initialized with prefetched configuration");
            }
        }
    }
}
