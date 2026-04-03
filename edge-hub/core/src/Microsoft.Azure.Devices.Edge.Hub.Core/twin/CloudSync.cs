// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class CloudSync : ICloudSync
    {
        readonly IConnectionManager connectionManager;
        readonly IMessageConverter<PropertyCollection> twinCollectionConverter;
        readonly IMessageConverter<TwinProperties> twinConverter;

        public CloudSync(
            IConnectionManager connectionManager,
            IMessageConverter<PropertyCollection> twinCollectionConverter,
            IMessageConverter<TwinProperties> twinConverter)
        {
            this.connectionManager = connectionManager;
            this.twinCollectionConverter = twinCollectionConverter;
            this.twinConverter = twinConverter;
        }

        public async Task<Option<TwinProperties>> GetTwin(string id)
        {
            try
            {
                Events.GettingTwin(id);
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                Option<TwinProperties> twin = await cloudProxy.Map(
                        async cp =>
                        {
                            IMessage twinMessage = await cp.GetTwinAsync();
                            TwinProperties twinValue = this.twinConverter.FromMessage(twinMessage);
                            Events.GetTwinSucceeded(id);
                            return Option.Some(twinValue);
                        })
                    .GetOrElse(() => Task.FromResult(Option.None<TwinProperties>()));
                return twin;
            }
            catch (Exception ex)
            {
                Events.ErrorGettingTwin(id, ex);
                return Option.None<TwinProperties>();
            }
        }

        public async Task<bool> UpdateReportedProperties(string id, PropertyCollection patch)
        {
            try
            {
                Events.UpdatingReportedProperties(id);
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                bool result = await cloudProxy.Map(
                        async cp =>
                        {
                            IMessage patchMessage = this.twinCollectionConverter.ToMessage(patch);
                            await cp.UpdateReportedPropertiesAsync(patchMessage);
                            Events.UpdatedReportedProperties(id);
                            return true;
                        })
                    .GetOrElse(() => Task.FromResult(false));
                return result;
            }
            catch (Exception ex)
            {
                Events.ErrorUpdatingReportedProperties(id, ex);
                return false;
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.TwinManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<StoringTwinManager>();

            enum EventIds
            {
                GettingTwin = IdStart + 70,
                GetTwinSucceeded,
                ErrorGettingTwin,
                UpdatingReportedProperties,
                UpdatedReportedProperties,
                ErrorUpdatingReportedProperties
            }

            public static void ErrorUpdatingReportedProperties(string id, Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorUpdatingReportedProperties, ex, $"Error updating reported properties for {id}");
            }

            public static void UpdatedReportedProperties(string id)
            {
                Log.LogInformation((int)EventIds.UpdatedReportedProperties, $"Updated reported properties for {id}");
            }

            public static void UpdatingReportedProperties(string id)
            {
                Log.LogDebug((int)EventIds.UpdatingReportedProperties, $"Updating reported properties for {id}");
            }

            public static void ErrorGettingTwin(string id, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorGettingTwin, ex, $"Error getting twin for {id}");
            }

            public static void GetTwinSucceeded(string id)
            {
                Log.LogDebug((int)EventIds.GetTwinSucceeded, $"Got twin for {id}");
            }

            public static void GettingTwin(string id)
            {
                Log.LogDebug((int)EventIds.GettingTwin, $"Getting twin for {id}");
            }
        }
    }
}
