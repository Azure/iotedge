// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt.Subscription;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    public class SessionStatePersistenceProvider : ISessionStatePersistenceProvider
    {
        readonly IConnectionManager connectionManager;

        public SessionStatePersistenceProvider(IConnectionManager connectionManager)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
        }

        public ISessionState Create(bool transient)
        {
            // set transient to false to get calls from Protocol Gateway when there are changes to the subscription
            return new SessionState(false);
        }

        public Task<ISessionState> GetAsync(IDeviceIdentity identity)
        {
            return Task.FromResult((ISessionState)null);
        }

        public async Task SetAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            var registrationSessionState = sessionState as SessionState;
            if (registrationSessionState == null)
            {
                return;
            }

            IReadOnlyList<ISubscriptionRegistration> registrations = registrationSessionState.SubscriptionRegistrations;

            Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(identity.Id);
            foreach (ISubscriptionRegistration subscriptionRegistration in registrations)
            {
                await cloudProxy.Map(cp => subscriptionRegistration.ProcessSubscriptionAsync(cp)).GetOrElse(Task.FromResult(true));
            }

            registrationSessionState.ClearRegistrations();
        }

        public Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            return TaskEx.Done;
        }
    }
}
