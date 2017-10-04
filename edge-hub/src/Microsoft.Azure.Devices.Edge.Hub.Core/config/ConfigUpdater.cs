// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;

    public class ConfigUpdater
    {
        readonly Router router;
        readonly IMessageStore messageStore;

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
                EdgeHubConfig edgeHubConfig = await configProvider.GetConfig();
                await this.UpdateRoutes(edgeHubConfig.Routes, false);
                this.UpdateStoreAndForwardConfig(edgeHubConfig.StoreAndForwardConfiguration);
                configProvider.SetConfigUpdatedCallback(this.UpdateConfig);
                Events.Initialized();
            }
            catch (Exception ex)
            {
                Events.InitializingError(ex);
            }
        }

        async Task UpdateConfig(EdgeHubConfig edgeHubConfig)
        {
            Preconditions.CheckNotNull(edgeHubConfig, nameof(edgeHubConfig));
            try
            {
                await this.UpdateRoutes(edgeHubConfig.Routes, true);
                this.UpdateStoreAndForwardConfig(edgeHubConfig.StoreAndForwardConfiguration);
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
            }
        }

        void UpdateStoreAndForwardConfig(StoreAndForwardConfiguration storeAndForwardConfiguration)
        {
            if (storeAndForwardConfiguration != null)
            {
                this.messageStore?.SetTimeToLive(TimeSpan.FromSeconds(storeAndForwardConfiguration.TimeToLiveSecs));
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
                Updated,
                UpdateError
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
                    FormattableString.Invariant($"Error updating Edge Hub configuration"));
            }
        }
    }
}
