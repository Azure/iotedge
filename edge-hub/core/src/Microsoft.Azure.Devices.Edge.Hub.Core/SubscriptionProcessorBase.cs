// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public abstract class SubscriptionProcessorBase : ISubscriptionProcessor
    {
        protected SubscriptionProcessorBase(IConnectionManager connectionManager)
        {
            this.ConnectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
        }

        protected IConnectionManager ConnectionManager { get; }

        public Task AddSubscription(string id, DeviceSubscription deviceSubscription)
        {
            Events.AddingSubscription(id, deviceSubscription);
            this.ConnectionManager.AddSubscription(id, deviceSubscription);
            this.HandleSubscriptions(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, true) });
            return Task.CompletedTask;
        }

        public Task RemoveSubscription(string id, DeviceSubscription deviceSubscription)
        {
            Events.RemovingSubscription(id, deviceSubscription);
            this.ConnectionManager.RemoveSubscription(id, deviceSubscription);
            this.HandleSubscriptions(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, false) });
            return Task.CompletedTask;
        }

        public Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            List<(DeviceSubscription, bool)> subscriptionsList = Preconditions.CheckNotNull(subscriptions, nameof(subscriptions)).ToList();
            Events.ProcessingSubscriptions(id, subscriptionsList);
            foreach ((DeviceSubscription deviceSubscription, bool addSubscription) in subscriptionsList)
            {
                if (addSubscription)
                {
                    this.ConnectionManager.AddSubscription(id, deviceSubscription);
                }
                else
                {
                    this.ConnectionManager.RemoveSubscription(id, deviceSubscription);
                }
            }

            this.HandleSubscriptions(id, subscriptionsList);
            return Task.CompletedTask;
        }

        protected abstract void HandleSubscriptions(string id, List<(DeviceSubscription, bool)> subscriptions);

        static class Events
        {
            const int IdStart = HubCoreEventIds.SubscriptionProcessor;
            static readonly ILogger Log = Logger.Factory.CreateLogger<SubscriptionProcessor>();

            enum EventIds
            {
                AddingSubscription = IdStart,
                RemovingSubscription,
                ProcessingSubscriptions
            }

            public static void AddingSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.AddingSubscription, Invariant($"Adding subscription {subscription} for client {id}."));
            }

            public static void RemovingSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.RemovingSubscription, Invariant($"Removing subscription {subscription} for client {id}."));
            }

            internal static void ProcessingSubscriptions(string id, List<(DeviceSubscription, bool)> subscriptionsList)
            {
                string subscriptions = string.Join(", ", subscriptionsList.Select(s => $"{s.Item1}"));
                Log.LogInformation((int)EventIds.ProcessingSubscriptions, Invariant($"Processing subscriptions {subscriptions} for client {id}."));
            }
        }
    }
}
