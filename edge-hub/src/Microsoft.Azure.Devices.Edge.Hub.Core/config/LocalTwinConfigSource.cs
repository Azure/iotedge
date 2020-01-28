// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class LocalTwinConfigSource : IConfigSource
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeHubConfigParser>();

        readonly ITwinManager twinManager;
        readonly IMessageConverter<Shared.Twin> twinMessageConverter;
        readonly string id;
        readonly Devices.Routing.Core.RouteFactory routeFactory;

        public LocalTwinConfigSource(string id, ITwinManager twinManager, IMessageConverter<Shared.Twin> messageConverter, Devices.Routing.Core.RouteFactory routeFactory)
        {
            this.id = Preconditions.CheckNotNull(id, nameof(id));
            this.twinManager = Preconditions.CheckNotNull(twinManager, nameof(twinManager));
            this.twinMessageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.routeFactory = Preconditions.CheckNotNull(routeFactory, nameof(routeFactory));
        }

        public async Task<Option<EdgeHubConfig>> GetConfig()
        {
            try
            {
                Option<IMessage> twinMessage = await this.twinManager.GetCachedTwinAsync(this.id);

                var config = twinMessage.FlatMap((message) =>
                {
                    Shared.Twin twin = this.twinMessageConverter.FromMessage(message);

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
                Log.LogWarning(HubCoreEventIds.LocalEdgeHubConfig, e, "Failed to get local config");
                return Option.None<EdgeHubConfig>();
            }
        }

        public void SetConfigUpdatedCallback(Func<EdgeHubConfig, Task> callback)
        {
        }
    }
}
