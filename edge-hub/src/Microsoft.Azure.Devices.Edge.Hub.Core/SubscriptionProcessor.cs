// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    /// <summary>
    /// The SubscriptionProcessor processes subscriptions from the client.
    /// When a subscription is received from the client, it is added to a queue
    /// and the client is sent an ACK right away. Then the subscriptions from the queue
    /// are processed one by one.
    /// This helps in the offline scenario where the GetCloudProxy could take up to 20 secs
    /// to return, that too with a negative result.
    /// Note that subscriptions are not stored in the SubscriptionProcessor - they are stored
    /// in the ConnectionManager.
    /// </summary>
    public class SubscriptionProcessor : ISubscriptionProcessor
    {
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4));

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

        public Task AddSubscription(string id, DeviceSubscription deviceSubscription)
        {
            Events.AddingSubscription(id, deviceSubscription);
            this.connectionManager.AddSubscription(id, deviceSubscription);
            this.AddToPendingSubscriptions(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, true) });
            return Task.CompletedTask;
        }

        public Task RemoveSubscription(string id, DeviceSubscription deviceSubscription)
        {
            Events.RemovingSubscription(id, deviceSubscription);
            this.connectionManager.RemoveSubscription(id, deviceSubscription);
            this.AddToPendingSubscriptions(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, false) });
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

        async Task ProcessSubscriptionWithRetry(string id, Option<ICloudProxy> cloudProxy, DeviceSubscription deviceSubscription, bool addSubscription)
        {
            Events.ProcessingSubscription(id, deviceSubscription);
            try
            {
                await ExecuteWithRetry(
                    () => this.ProcessSubscription(id, cloudProxy, deviceSubscription, addSubscription),
                    r => Events.ErrorProcessingSubscription(id, deviceSubscription, addSubscription, r));
            }
            catch (Exception ex)
            {
                Events.ErrorProcessingSubscription(id, deviceSubscription, addSubscription, ex);
            }
        }

        async Task ProcessSubscription(string id, Option<ICloudProxy> cloudProxy, DeviceSubscription deviceSubscription, bool addSubscription)
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
                        await this.ProcessSubscriptionWithRetry(id, cloudProxy, subscription.Key, subscription.Value);
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
                    await this.ProcessSubscriptionWithRetry(id, cloudProxy, result.deviceSubscription, result.addSubscription);
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

        static Task ExecuteWithRetry(Func<Task> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            static readonly ISet<Type> NonTransientExceptions = new HashSet<Type>
            {
                typeof(ArgumentException),
                typeof(UnauthorizedException)
            };

            public bool IsTransient(Exception ex) => !NonTransientExceptions.Contains(ex.GetType());
        }

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

            public static void ErrorProcessingSubscription(string id, DeviceSubscription subscription, bool addSubscription, RetryingEventArgs r)
            {
                Exception ex = r.LastException;
                string operation = addSubscription ? "adding" : "removing";
                int retryCount = r.CurrentRetryCount;
                if (ex.HasTimeoutException())
                {
                    Log.LogDebug((int)EventIds.ErrorAddingSubscription, ex, Invariant($"Timed out while processing subscription {subscription} for client {id} on attempt {retryCount}."));
                }
                else
                {
                    Log.LogDebug((int)EventIds.ErrorRemovingSubscription, ex, Invariant($"Error {operation} subscription {subscription} for client {id} on attempt {retryCount}."));
                }
            }

            public static void ErrorProcessingSubscription(string id, DeviceSubscription subscription, bool addSubscription, Exception ex)
            {
                string operation = addSubscription ? "adding" : "removing";
                if (ex.HasTimeoutException())
                {
                    Log.LogDebug((int)EventIds.ErrorAddingSubscription, ex, Invariant($"Timed out while processing subscription {subscription} for client {id}. Will try to add subscription when the device is online."));
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
