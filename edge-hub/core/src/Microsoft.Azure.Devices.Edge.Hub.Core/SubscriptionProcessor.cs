// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
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
    public class SubscriptionProcessor : SubscriptionProcessorBase, IDisposable
    {
        static readonly TimeSpan RecoveryMaxBackoff = TimeSpan.FromSeconds(30);
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4));

        static readonly ShouldRetry RecoveryShouldRetry =
            new ExponentialBackoff(int.MaxValue, TimeSpan.FromSeconds(1), RecoveryMaxBackoff, TimeSpan.FromSeconds(1)).GetShouldRetry();

        readonly ConcurrentDictionary<string, ConcurrentQueue<(DeviceSubscription, bool)>> pendingSubscriptions;
        readonly ConcurrentDictionary<string, ClientState> clientStates = new ConcurrentDictionary<string, ClientState>();
        readonly IInvokeMethodHandler invokeMethodHandler;
        readonly IDeviceConnectivityManager deviceConnectivityManager;
        readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        readonly CancellationToken shutdownToken;
        int disposed;

        public SubscriptionProcessor(IConnectionManager connectionManager, IInvokeMethodHandler invokeMethodHandler, IDeviceConnectivityManager deviceConnectivityManager)
            : base(connectionManager)
        {
            this.deviceConnectivityManager = Preconditions.CheckNotNull(deviceConnectivityManager, nameof(deviceConnectivityManager));
            this.invokeMethodHandler = Preconditions.CheckNotNull(invokeMethodHandler, nameof(invokeMethodHandler));
            this.pendingSubscriptions = new ConcurrentDictionary<string, ConcurrentQueue<(DeviceSubscription, bool)>>();
            this.shutdownToken = this.shutdown.Token;
            connectionManager.DeviceConnected += this.ClientConnectionToEdgeHubEstablished;
            this.deviceConnectivityManager.DeviceConnected += this.CloudConnectivityEstablished;
            connectionManager.CloudConnectionEstablished += this.ClientConnectionToCloudEstablished;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 0)
            {
                this.ConnectionManager.DeviceConnected -= this.ClientConnectionToEdgeHubEstablished;
                this.deviceConnectivityManager.DeviceConnected -= this.CloudConnectivityEstablished;
                this.ConnectionManager.CloudConnectionEstablished -= this.ClientConnectionToCloudEstablished;
                this.shutdown.Cancel();
                this.shutdown.Dispose();
            }
        }

        protected override void HandleSubscriptions(string id, List<(DeviceSubscription, bool)> subscriptions) =>
            this.AddToPendingSubscriptions(id, subscriptions);

        static TimeSpan GetRecoveryDelay(int retryAttempt)
        {
            return RecoveryShouldRetry(retryAttempt - 1, null, out TimeSpan delay)
                ? delay
                : RecoveryMaxBackoff;
        }

        static Task ExecuteWithRetry(Func<Task> func, Action<RetryingEventArgs> onRetry, CancellationToken cancellationToken)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func, cancellationToken);
        }

        async Task<bool> ProcessSubscriptionWithRetry(string id, ICloudProxy cloudProxy, DeviceSubscription deviceSubscription, bool addSubscription)
        {
            Events.ProcessingSubscription(id, deviceSubscription);
            try
            {
                await ExecuteWithRetry(
                    () => this.ProcessSubscription(id, cloudProxy, deviceSubscription, addSubscription),
                    r =>
                        {
                            Metrics.AddRetryOperation(id, addSubscription ? "AddSubscription" : "RemoveSubscription");
                            Events.ErrorProcessingSubscription(id, deviceSubscription, addSubscription, r);
                        },
                    this.shutdownToken);
                return true;
            }
            catch (OperationCanceledException) when (this.shutdownToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Events.ErrorProcessingSubscription(id, deviceSubscription, addSubscription, ex);
                return false;
            }
        }

        async Task ProcessSubscription(string id, ICloudProxy cloudProxy, DeviceSubscription deviceSubscription, bool addSubscription)
        {
            switch (deviceSubscription)
            {
                case DeviceSubscription.C2D:
                    if (addSubscription)
                    {
                        await cloudProxy.StartListening();
                    }
                    else
                    {
                        await cloudProxy.StopListening();
                    }

                    break;

                case DeviceSubscription.DesiredPropertyUpdates:
                    await (addSubscription ? cloudProxy.SetupDesiredPropertyUpdatesAsync() : cloudProxy.RemoveDesiredPropertyUpdatesAsync());
                    break;

                case DeviceSubscription.Methods:
                    if (addSubscription)
                    {
                        await cloudProxy.SetupCallMethodAsync();
                        await this.invokeMethodHandler.ProcessInvokeMethodSubscription(id);
                    }
                    else
                    {
                        await cloudProxy.RemoveCallMethodAsync();
                    }

                    break;

                case DeviceSubscription.TwinResponse:
                    // No need for handling addSubscription == true, because the SDK subscribes automatically,
                    // and because of that the rest of the CloudProxy implementations were built that way later.
                    if (!addSubscription)
                    {
                        await cloudProxy.RemoveTwinResponseAsync();
                    }

                    break;

                case DeviceSubscription.ModuleMessages:
                case DeviceSubscription.Unknown:
                    // No Action required
                    break;
            }
        }

        void CloudConnectivityEstablished(object sender, EventArgs eventArgs)
        {
            Events.DeviceConnectedProcessingSubscriptions();
            foreach (IIdentity identity in this.ConnectionManager.GetConnectedClients())
            {
                Events.ProcessingSubscriptionsOnDeviceConnectedToCloud(identity);
                this.Signal(identity.Id);
            }
        }

        void ClientConnectionToCloudEstablished(object sender, IIdentity identity)
        {
            Events.ClientConnectedToCloudProcessingSubscriptions(identity);
            this.Signal(identity.Id);
        }

        void ClientConnectionToEdgeHubEstablished(object sender, IIdentity identity)
        {
            Events.ClientConnectedToEdgeHubProcessingSubscriptions(identity);
            this.Signal(identity.Id);
        }

        void Signal(string id)
        {
            if (Volatile.Read(ref this.disposed) != 0)
            {
                return;
            }

            ClientState state = this.clientStates.GetOrAdd(id, _ => new ClientState());
            if (state.Signal())
            {
                _ = this.ProcessSubscriptionsAsync(id, state);
            }
        }

        async Task ProcessSubscriptionsAsync(string id, ClientState state)
        {
            try
            {
                while (true)
                {
                    Events.ProcessingSubscriptions(id);
                    state.StartPass();
                    bool retry;
                    try
                    {
                        Option<ICloudProxy> cloudProxy = await this.ConnectionManager.GetCloudConnection(id).WaitAsync(this.shutdownToken);
                        this.shutdownToken.ThrowIfCancellationRequested();
                        if (cloudProxy.HasValue)
                        {
                            retry = !await this.ApplySubscriptions(id, cloudProxy.OrDefault());
                        }
                        else
                        {
                            Events.ProcessingSubscriptionsNoCloudProxy(id);
                            retry = true;
                        }
                    }
                    catch (Exception ex) when (!ex.IsFatal() && !(ex is OperationCanceledException && this.shutdownToken.IsCancellationRequested))
                    {
                        Events.ErrorProcessingSubscriptions(ex, id);
                        retry = true;
                    }

                    if (!retry)
                    {
                        state.ResetRetryAttempt();
                        if (state.TryComplete())
                        {
                            return;
                        }

                        continue;
                    }

                    if (!this.ConnectionManager.GetDeviceConnection(id).HasValue)
                    {
                        if (state.TryComplete())
                        {
                            return;
                        }

                        continue;
                    }

                    await Task.Delay(GetRecoveryDelay(state.IncrementRetryAttempt()), this.shutdownToken);
                }
            }
            catch (OperationCanceledException) when (this.shutdownToken.IsCancellationRequested)
            {
                state.Abort();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.ErrorProcessingSubscriptions(ex, id);
                if (state.Abort())
                {
                    this.Signal(id);
                }
            }
        }

        async Task<bool> ApplySubscriptions(string id, ICloudProxy cloudProxy)
        {
            var processedSubscriptions = new Dictionary<DeviceSubscription, bool>();
            bool succeeded = true;
            ConcurrentQueue<(DeviceSubscription, bool)> clientSubscriptionsQueue = this.GetClientSubscriptionsQueue(id);
            while (clientSubscriptionsQueue.TryPeek(out (DeviceSubscription deviceSubscription, bool addSubscription) result))
            {
                bool operationSucceeded = await this.ProcessSubscriptionWithRetry(id, cloudProxy, result.deviceSubscription, result.addSubscription);
                processedSubscriptions[result.deviceSubscription] = operationSucceeded;
                succeeded &= operationSucceeded;

                clientSubscriptionsQueue.TryDequeue(out _);
            }

            Option<IReadOnlyDictionary<DeviceSubscription, bool>> subscriptions = this.ConnectionManager.GetSubscriptions(id);
            await subscriptions.ForEachAsync(
                async s =>
                {
                    foreach (KeyValuePair<DeviceSubscription, bool> subscription in s)
                    {
                        if (!processedSubscriptions.TryGetValue(subscription.Key, out bool operationSucceeded) || !operationSucceeded)
                        {
                            succeeded &= await this.ProcessSubscriptionWithRetry(id, cloudProxy, subscription.Key, subscription.Value);
                        }
                    }
                });
            return succeeded;
        }

        void AddToPendingSubscriptions(string id, List<(DeviceSubscription, bool)> subscriptions)
        {
            ConcurrentQueue<(DeviceSubscription, bool)> clientSubscriptionsQueue = this.GetClientSubscriptionsQueue(id);
            subscriptions.ForEach(s => clientSubscriptionsQueue.Enqueue(s));
            this.Signal(id);
        }

        ConcurrentQueue<(DeviceSubscription, bool)> GetClientSubscriptionsQueue(string id)
            => this.pendingSubscriptions.GetOrAdd(id, new ConcurrentQueue<(DeviceSubscription, bool)>());

        sealed class ClientState
        {
            const int Idle = 0;
            const int Running = 1;
            const int Pending = 2;
            int status;
            int retryAttempt;

            public bool Signal() => Interlocked.Exchange(ref this.status, Pending) == Idle;

            public void StartPass() => Interlocked.CompareExchange(ref this.status, Running, Pending);

            public bool TryComplete() => Interlocked.CompareExchange(ref this.status, Idle, Running) == Running;

            public int IncrementRetryAttempt() => ++this.retryAttempt;

            public void ResetRetryAttempt() => this.retryAttempt = 0;

            public bool Abort() => Interlocked.Exchange(ref this.status, Idle) == Pending;
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
                ProcessingSubscriptions,
                ProcessingSubscription,
                DeviceConnectedToEdgeHubProcessingSubscription,
                ClientConnectedProcessingSubscriptions,
                ProcessingSubscriptionsNoCloudProxy
            }

            public static void ErrorProcessingSubscriptions(Exception ex, IIdentity identity)
                => ErrorProcessingSubscriptions(ex, identity.Id);

            public static void ErrorProcessingSubscriptions(Exception ex, string id)
            {
                if (ex.HasTimeoutException())
                {
                    Log.LogDebug((int)EventIds.ErrorProcessingSubscriptions, ex, Invariant($"Timed out while processing subscriptions for client {id}. Will retry subscription recovery."));
                }
                else
                {
                    Log.LogWarning((int)EventIds.ErrorProcessingSubscriptions, ex, Invariant($"Error processing subscriptions for client {id}."));
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

            public static void ProcessingSubscriptionsOnDeviceConnectedToCloud(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.ProcessingSubscriptions, Invariant($"Processing subscriptions for client {identity.Id} on device connected to cloud."));
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
                Log.LogWarning((int)EventIds.ErrorProcessingSubscriptions, e, Invariant($"Error processing subscriptions for connected clients."));
            }

            public static void ClientConnectedToCloudProcessingSubscriptions(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.ClientConnectedProcessingSubscriptions, Invariant($"Client {identity.Id} connected to cloud, processing existing subscriptions."));
            }

            public static void ClientConnectedToEdgeHubProcessingSubscriptions(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.DeviceConnectedToEdgeHubProcessingSubscription, Invariant($"Client {identity.Id} connected to edgeHub, processing existing subscriptions."));
            }

            public static void ProcessingSubscriptions(string id)
            {
                Log.LogInformation((int)EventIds.ProcessingSubscription, Invariant($"Processing pending subscriptions for {id}"));
            }

            public static void ProcessingSubscriptionsNoCloudProxy(string id)
            {
                Log.LogInformation((int)EventIds.ProcessingSubscriptionsNoCloudProxy, Invariant($"Processing pending subscriptions for {id}, but no cloud proxy was found"));
            }
        }

        static class Metrics
        {
            static readonly IMetricsCounter RetriesCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                "operation_retry",
                "Operation retries",
                new List<string> { "id", "operation", MetricsConstants.MsTelemetry });

            public static void AddRetryOperation(string id, string operation) => RetriesCounter.Increment(1, new[] { id, operation, bool.TrueString });
        }
    }
}
