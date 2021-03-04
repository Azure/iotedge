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
        IIdentity identity;

        public BrokeredCloudConnection(BrokeredCloudProxy cloudProxy)
        {
            Preconditions.CheckNotNull(cloudProxy);

            this.IsActive = true;
            this.CloudProxy = Option.Some(cloudProxy as ICloudProxy);
            this.identity = cloudProxy.Identity;
        }

        public Option<ICloudProxy> CloudProxy { get; }

        public bool IsActive { get; private set; }

        public async Task<bool> CloseAsync()
        {
            this.IsActive = false;
            var result = default(bool);

            try
            {
                result = await this.CloudProxy.Match(
                                        cp => cp.CloseAsync(),
                                        () => Task.FromResult(true));
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
