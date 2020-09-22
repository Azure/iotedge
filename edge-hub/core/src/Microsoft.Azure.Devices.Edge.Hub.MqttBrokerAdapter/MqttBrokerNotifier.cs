// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class MqttBrokerNotifier
    {
        static readonly IEnumerable<BrokerMessage> EdgeHubReadyNotification = new[] { new BrokerMessage("$ehc/ready", "\"EdgeHub is ready to serve.\"") };
        static readonly IEnumerable<BrokerMessage> EmptyNotification = new BrokerMessage[0];
        readonly NotificationHandler<bool> notificationHandler;
        readonly Task<IDeviceScopeIdentitiesCache> deviceScopeIdentitiesCacheSupplier;
        readonly Task<AuthAgentProtocolHead> authAgentProtocolHeadSupplier;

        public MqttBrokerNotifier(IMqttBrokerConnector mqttBrokerConnector, Task<IDeviceScopeIdentitiesCache> deviceScopeIdentitiesCacheSupplier, Task<AuthAgentProtocolHead> authAgentProtocolHeadSupplier)
        {
            this.deviceScopeIdentitiesCacheSupplier = deviceScopeIdentitiesCacheSupplier;
            this.authAgentProtocolHeadSupplier = authAgentProtocolHeadSupplier;
            this.notificationHandler = new NotificationHandler<bool>(this.ConvertNotificationToMessagesAsync, storedNotificationRetriever: this.ConvertStoredNotificationsToMessagesAsync);
            this.notificationHandler.SetConnector(mqttBrokerConnector);
        }

        async Task<IEnumerable<BrokerMessage>> ConvertStoredNotificationsToMessagesAsync()
        {
            var authAgentProtocolHead = await this.authAgentProtocolHeadSupplier;
            var deviceScopeIdentitiesCache = await this.deviceScopeIdentitiesCacheSupplier;
            await Task.WhenAll(deviceScopeIdentitiesCache.WaitForIntialialCachingCompleteAsync(), authAgentProtocolHead.WaitForStartAsync());
            return EdgeHubReadyNotification;
        }

        Task<IEnumerable<BrokerMessage>> ConvertNotificationToMessagesAsync(bool _) => Task.FromResult(EmptyNotification);
    }
}
