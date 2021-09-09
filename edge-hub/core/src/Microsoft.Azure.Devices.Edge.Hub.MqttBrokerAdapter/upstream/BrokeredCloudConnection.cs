// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class BrokeredCloudConnection : ICloudConnection
    {
        readonly IIdentity identity;

        public BrokeredCloudConnection(IIdentity identity, ICloudProxy cloudProxy)
        {
            Preconditions.CheckNotNull(identity);
            Preconditions.CheckNotNull(cloudProxy);

            this.identity = identity;
            this.CloudProxy = Option.Some(cloudProxy);
        }

        public Option<ICloudProxy> CloudProxy { get; }

        public bool IsActive => this.CloudProxy.Map(cp => cp.IsActive).GetOrElse(false);

        public async Task<bool> CloseAsync()
        {
            var result = default(bool);

            try
            {
                result = await this.CloudProxy
                                        .Map(cp => cp.CloseAsync())
                                        .GetOrElse(Task.FromResult(true));
            }
            catch (Exception e)
            {
                result = false;
                Events.ErrorClosingCloudProxy(e, this.identity.Id);
            }

            return result;
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.BrokeredCloudConnection;
            static readonly ILogger Log = Logger.Factory.CreateLogger<BrokeredCloudConnection>();

            enum EventIds
            {
                ErrorClosingCloudProxy = IdStart,
            }

            public static void ErrorClosingCloudProxy(Exception e, string id) => Log.LogError((int)EventIds.ErrorClosingCloudProxy, e, $"Error closing cloud-proxy for {id}");
        }
    }
}
