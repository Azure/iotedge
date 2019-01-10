// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class CloudSync : ICloudSync
    {
        readonly IConnectionManager connectionManager;
        readonly IMessageConverter<TwinCollection> twinCollectionConverter;
        readonly IMessageConverter<Twin> twinConverter;

        public CloudSync(
            IConnectionManager connectionManager,
            IMessageConverter<TwinCollection> twinCollectionConverter,
            IMessageConverter<Twin> twinConverter)
        {
            this.connectionManager = connectionManager;
            this.twinCollectionConverter = twinCollectionConverter;
            this.twinConverter = twinConverter;
        }

        public async Task<Option<Twin>> GetTwin(string id)
        {
            try
            {
                Events.GettingTwin(id);
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                Option<Twin> twin = await cloudProxy.Map(
                        async cp =>
                        {
                            IMessage twinMessage = await cp.GetTwinAsync();
                            Twin twinValue = this.twinConverter.FromMessage(twinMessage);
                            Events.GetTwinSucceeded(id);
                            return Option.Some(twinValue);
                        })
                    .GetOrElse(() => Task.FromResult(Option.None<Twin>()));
                return twin;
            }
            catch (Exception ex)
            {
                Events.ErrorGettingTwin(id, ex);
                return Option.None<Twin>();
            }
        }

        public async Task<bool> UpdateReportedProperties(string id, TwinCollection patch)
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
            static readonly ILogger Log = Logger.Factory.CreateLogger<StoringTwinManager>();
            const int IdStart = HubCoreEventIds.TwinManager;

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
