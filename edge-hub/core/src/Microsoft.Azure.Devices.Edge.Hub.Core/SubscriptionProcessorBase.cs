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
            var hasChanged = this.ConnectionManager.AddSubscription(id, deviceSubscription);

            if (hasChanged)
            {
                this.HandleSubscriptions(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, true) });
            }
            else
            {
                Events.UnchangedSubscription(id, deviceSubscription);
            }

            return Task.CompletedTask;
        }

        public Task RemoveSubscription(string id, DeviceSubscription deviceSubscription)
        {
            Events.RemovingSubscription(id, deviceSubscription);
            var hasChanged = this.ConnectionManager.RemoveSubscription(id, deviceSubscription);

            if (hasChanged)
            {
                this.HandleSubscriptions(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, false) });
            }
            else
            {
                Events.UnchangedSubscription(id, deviceSubscription);
            }

            return Task.CompletedTask;
        }

        public Task RemoveSubscriptions(string id)
        {
            Events.RemovingSubscriptions(id);
            var toRemove = this.ConnectionManager.RemoveSubscriptions(id);

            if (toRemove.Count > 0)
            {
                this.HandleSubscriptions(id, toRemove.Select(v => (v, false)).ToList());
            }
            else
            {
                Events.NothingToRemove(id);
            }

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
                RemovingSubscriptions,
                ProcessingSubscriptions,
                UnchangedSubscription,
                NothingToRemove
            }

            public static void AddingSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.AddingSubscription, Invariant($"Adding subscription {subscription} for client {id}."));
            }

            public static void RemovingSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.RemovingSubscription, Invariant($"Removing subscription {subscription} for client {id}."));
            }

            public static void RemovingSubscriptions(string id)
            {
                Log.LogDebug((int)EventIds.RemovingSubscription, Invariant($"Removing all subscriptions for client {id}."));
            }

            public static void UnchangedSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.UnchangedSubscription, Invariant($"subscription {subscription} did not change for client {id}, no action has been taken."));
            }

            public static void NothingToRemove(string id)
            {
                Log.LogDebug((int)EventIds.NothingToRemove, Invariant($"Client {id} had no subscriptions, no action has been taken."));
            }

            internal static void ProcessingSubscriptions(string id, List<(DeviceSubscription, bool)> subscriptionsList)
            {
                string subscriptions = string.Join(", ", subscriptionsList.Select(s => $"{s.Item1}"));
                Log.LogInformation((int)EventIds.ProcessingSubscriptions, Invariant($"Processing subscriptions {subscriptions} for client {id}."));
            }
        }
    }
}
