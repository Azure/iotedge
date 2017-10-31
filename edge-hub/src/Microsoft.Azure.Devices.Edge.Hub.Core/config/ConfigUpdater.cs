// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;

    public class ConfigUpdater
    {
        readonly Router router;
        readonly IMessageStore messageStore;
        readonly AsyncLock updateLock = new AsyncLock();

        public ConfigUpdater(Router router, IMessageStore messageStore)
        {
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.messageStore = messageStore;
        }

        public async Task Init(IConfigSource configProvider)
        {
            Preconditions.CheckNotNull(configProvider, nameof(configProvider));
            try
            {
                using (await this.updateLock.LockAsync())
                {
                    configProvider.SetConfigUpdatedCallback(this.UpdateConfig);
                    Option<EdgeHubConfig> edgeHubConfig = await configProvider.GetConfig();
                    await edgeHubConfig.Match(
                        async config =>
                        {
                            await this.UpdateRoutes(config.Routes, false);
                            this.UpdateStoreAndForwardConfig(config.StoreAndForwardConfiguration);
                            Events.Initialized();
                        },
                        () =>
                        {
                            Events.EmptyConfigReceived();
                            return Task.CompletedTask;
                        });
                }
            }
            catch (Exception ex)
            {
                Events.InitializingError(ex);
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

        async Task UpdateRoutes(IDictionary<string, Route> routes, bool replaceExisting)
        {
            if (routes != null)
            {
                ISet<Route> routeSet = new HashSet<Route>(routes.Values);
                if (replaceExisting)
                {
                    await this.router.ReplaceRoutes(routeSet);
                }
                else
                {
                    foreach (Route route in routes.Values)
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
                this.messageStore?.SetTimeToLive(TimeSpan.FromSeconds(storeAndForwardConfiguration.TimeToLiveSecs));
                Events.UpdatedStoreAndForwardConfiguration();
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConfigUpdater>();
            const int IdStart = HubCoreEventIds.ConfigUpdater;

            enum EventIds
            {
                Initialized = IdStart,
                InitializeError,
                UpdateError,
                UpdatingConfig,
                UpdatedRoutes,
                UpdatedStoreAndForwardConfig,
                EmptyConfig
            }

            internal static void Initialized()
            {
                Log.LogInformation((int)EventIds.Initialized, FormattableString.Invariant($"Initialized Edge Hub configuration"));
            }

            internal static void InitializingError(Exception ex)
            {
                Log.LogError((int)EventIds.InitializeError, ex,
                    FormattableString.Invariant($"Error initializing Edge Hub configuration"));
            }

            internal static void UpdateError(Exception ex)
            {
                Log.LogError((int)EventIds.UpdateError, ex,
                    FormattableString.Invariant($"Error updating EdgeHub configuration"));
            }

            internal static void UpdatingConfig()
            {
                Log.LogInformation((int)EventIds.UpdatingConfig, "Setting/updating EdgeHub configuration");
            }

            internal static void RoutesUpdated(IDictionary<string, Route> routes)
            {
                int count = routes.Keys.Count;
                if (count > 0)
                {
                    string routeNames = string.Join(",", routes.Keys);
                    Log.LogInformation((int)EventIds.UpdatedRoutes, $"Set the following {count} route(s) in edge hub - {routeNames}");
                }
                else
                {
                    Log.LogInformation((int)EventIds.UpdatedRoutes, "Set 0 routes in the EdgeHub");
                }
            }

            internal static void UpdatedStoreAndForwardConfiguration()
            {
                Log.LogInformation((int)EventIds.UpdatedStoreAndForwardConfig, "Updated StoreAndForward configuration in the EdgeHub");
            }

            internal static void EmptyConfigReceived()
            {
                Log.LogWarning((int)EventIds.EmptyConfig, FormattableString.Invariant($"Empty Edge Hub configuration received."));
            }
        }
    }
}
