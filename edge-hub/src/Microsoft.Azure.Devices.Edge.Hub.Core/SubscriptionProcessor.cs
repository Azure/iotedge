// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class SubscriptionProcessor : ISubscriptionProcessor
    {
        readonly IConnectionManager connectionManager;
        readonly ConcurrentDictionary<string, ConcurrentQueue<(DeviceSubscription, bool)>> pendingSubscriptions;
        readonly ActionBlock<string> processSubscriptionsBlock;
        readonly IInvokeMethodHandler invokeMethodHandler;

        public SubscriptionProcessor(IConnectionManager connectionManager, IInvokeMethodHandler invokeMethodHandler, IDeviceConnectivityManager deviceConnectivityManager)
        {
            Preconditions.CheckNotNull(deviceConnectivityManager, nameof(deviceConnectivityManager));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.invokeMethodHandler = Preconditions.CheckNotNull(invokeMethodHandler, nameof(invokeMethodHandler));
            this.pendingSubscriptions = new ConcurrentDictionary<string, ConcurrentQueue<(DeviceSubscription, bool)>>();
            this.processSubscriptionsBlock = new ActionBlock<string>(this.ProcessPendingSubscriptions);
            deviceConnectivityManager.DeviceConnected += this.DeviceConnected;
        }

        public Task ProcessSubscription(string id, DeviceSubscription deviceSubscription, bool addSubscription)
        {
            if (addSubscription)
            {
                Events.AddingSubscription(id, deviceSubscription);
                this.connectionManager.AddSubscription(id, deviceSubscription);
            }
            else
            {
                Events.RemovingSubscription(id, deviceSubscription);
                this.connectionManager.RemoveSubscription(id, deviceSubscription);
            }

            this.AddToPendingSubscriptions(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, addSubscription) });
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
                    this.connectionManager.AddSubscription(id, deviceSubscription);
                }
                else
                {
                    this.connectionManager.RemoveSubscription(id, deviceSubscription);
                }
            }

            this.AddToPendingSubscriptions(id, subscriptionsList);
            return Task.CompletedTask;
        }

        internal async Task ProcessSubscription(string id, Option<ICloudProxy> cloudProxy, DeviceSubscription deviceSubscription, bool addSubscription)
        {
            Events.ProcessingSubscription(id, deviceSubscription);
            try
            {
                switch (deviceSubscription)
                {
                    case DeviceSubscription.C2D:
                        if (addSubscription)
                        {
                            cloudProxy.ForEach(c => c.StartListening());
                        }

                        break;

                    case DeviceSubscription.DesiredPropertyUpdates:
                        await cloudProxy.ForEachAsync(c => addSubscription ? c.SetupDesiredPropertyUpdatesAsync() : c.RemoveDesiredPropertyUpdatesAsync());
                        break;

                    case DeviceSubscription.Methods:
                        if (addSubscription)
                        {
                            await cloudProxy.ForEachAsync(c => c.SetupCallMethodAsync());
                            await this.invokeMethodHandler.ProcessInvokeMethodSubscription(id);
                        }
                        else
                        {
                            await cloudProxy.ForEachAsync(c => c.RemoveCallMethodAsync());
                        }

                        break;

                    case DeviceSubscription.ModuleMessages:
                    case DeviceSubscription.TwinResponse:
                    case DeviceSubscription.Unknown:
                        // No Action required
                        break;
                }
            }
            catch (Exception ex)
            {
                Events.ErrorProcessingSubscription(ex, id, deviceSubscription, addSubscription);
            }
        }

        async void DeviceConnected(object sender, EventArgs eventArgs)
        {
            Events.DeviceConnectedProcessingSubscriptions();
            try
            {
                IEnumerable<IIdentity> connectedClients = this.connectionManager.GetConnectedClients().ToList();
                foreach (IIdentity identity in connectedClients)
                {
                    try
                    {
                        Events.ProcessingSubscriptions(identity);
                        await this.ProcessExistingSubscriptions(identity.Id);
                    }
                    catch (Exception e)
                    {
                        Events.ErrorProcessingSubscriptions(e, identity);
                    }
                }
            }
            catch (Exception e)
            {
                Events.ErrorProcessingSubscriptions(e);
            }
        }

        async Task ProcessExistingSubscriptions(string id)
        {
            Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
            Option<IReadOnlyDictionary<DeviceSubscription, bool>> subscriptions = this.connectionManager.GetSubscriptions(id);
            await subscriptions.ForEachAsync(
                async s =>
                {
                    foreach (KeyValuePair<DeviceSubscription, bool> subscription in s)
                    {
                        await this.ProcessSubscription(id, cloudProxy, subscription.Key, subscription.Value);
                    }
                });
        }

        async Task ProcessPendingSubscriptions(string id)
        {
            ConcurrentQueue<(DeviceSubscription, bool)> clientSubscriptionsQueue = this.GetClientSubscriptionsQueue(id);
            if (!clientSubscriptionsQueue.IsEmpty)
            {
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                while (clientSubscriptionsQueue.TryDequeue(out (DeviceSubscription deviceSubscription, bool addSubscription) result))
                {
                    await this.ProcessSubscription(id, cloudProxy, result.deviceSubscription, result.addSubscription);
                }
            }
        }

        void AddToPendingSubscriptions(string id, List<(DeviceSubscription, bool)> subscriptions)
        {
            ConcurrentQueue<(DeviceSubscription, bool)> clientSubscriptionsQueue = this.GetClientSubscriptionsQueue(id);
            subscriptions.ForEach(s => clientSubscriptionsQueue.Enqueue(s));
            this.processSubscriptionsBlock.Post(id);
        }

        ConcurrentQueue<(DeviceSubscription, bool)> GetClientSubscriptionsQueue(string id)
            => this.pendingSubscriptions.GetOrAdd(id, new ConcurrentQueue<(DeviceSubscription, bool)>());

        static class Events
        {
            const int IdStart = HubCoreEventIds.SubscriptionProcessor;
            static readonly ILogger Log = Logger.Factory.CreateLogger<SubscriptionProcessor>();

            enum EventIds
            {
                ErrorProcessingSubscriptions = IdStart,
                ErrorRemovingSubscription,
                ErrorAddingSubscription,
                AddingSubscription,
                RemovingSubscription,
                ProcessingSubscriptions,
                ProcessingSubscription
            }

            public static void ErrorProcessingSubscriptions(Exception ex, IIdentity identity)
            {
                if (ex.HasTimeoutException())
                {
                    Log.LogDebug((int)EventIds.ErrorProcessingSubscriptions, ex, Invariant($"Timed out while processing subscriptions for client {identity.Id}. Will try again when connected."));
                }
                else
                {
                    Log.LogWarning((int)EventIds.ErrorProcessingSubscriptions, ex, Invariant($"Error processing subscriptions for client {identity.Id}."));
                }
            }

            public static void ErrorProcessingSubscription(Exception ex, string id, DeviceSubscription subscription, bool addSubscription)
            {
                string operation = addSubscription ? "adding" : "removing";
                if (ex.HasTimeoutException())
                {
                    Log.LogDebug((int)EventIds.ErrorAddingSubscription, ex, Invariant($"Timed out while {operation} subscription {subscription} for client {id}. Will try again when connected."));
                }
                else
                {
                    Log.LogWarning((int)EventIds.ErrorRemovingSubscription, ex, Invariant($"Error {operation} subscription {subscription} for client {id}."));
                }
            }

            public static void AddingSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.AddingSubscription, Invariant($"Adding subscription {subscription} for client {id}."));
            }

            public static void RemovingSubscription(string id, DeviceSubscription subscription)
            {
                Log.LogDebug((int)EventIds.RemovingSubscription, Invariant($"Removing subscription {subscription} for client {id}."));
            }

            public static void ProcessingSubscriptions(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.ProcessingSubscriptions, Invariant($"Processing subscriptions for client {identity.Id}."));
            }

            public static void ProcessingSubscription(string id, DeviceSubscription deviceSubscription)
            {
                Log.LogDebug((int)EventIds.ProcessingSubscription, Invariant($"Processing subscription {deviceSubscription} for client {id}."));
            }

            internal static void DeviceConnectedProcessingSubscriptions()
            {
                Log.LogInformation((int)EventIds.ProcessingSubscription, Invariant($"Device connected to cloud, processing subscriptions for connected clients."));
            }

            internal static void ErrorProcessingSubscriptions(Exception e)
            {
                Log.LogWarning((int)EventIds.ProcessingSubscription, e, Invariant($"Error processing subscriptions for connected clients."));
            }

            internal static void ProcessingSubscriptions(string id, List<(DeviceSubscription, bool)> subscriptionsList)
            {
                string subscriptions = string.Join(", ", subscriptionsList.Select(s => $"{s.Item1}"));
                Log.LogInformation((int)EventIds.ProcessingSubscription, Invariant($"Processing subscriptions {subscriptions} for client {id}."));
            }
        }
    }
}
