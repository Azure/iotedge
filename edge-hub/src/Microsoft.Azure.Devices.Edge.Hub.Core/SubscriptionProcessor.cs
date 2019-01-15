// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ISubscriptionProcessor
    {
        Task AddSubscription(string id, DeviceSubscription deviceSubscription);

        Task RemoveSubscription(string id, DeviceSubscription deviceSubscription);

        Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions);
    }

    public class SubscriptionProcessor
    {
        readonly IConnectionManager connectionManager;
        readonly ConcurrentDictionary<string, ConcurrentQueue<(DeviceSubscription, bool)>> subscriptionsToProcess;
        readonly ActionBlock<string> processSubscriptionsBlock;
        readonly IInvokeMethodHandler invokeMethodHandler;

        public SubscriptionProcessor(IConnectionManager connectionManager, IInvokeMethodHandler invokeMethodHandler)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.invokeMethodHandler = Preconditions.CheckNotNull(invokeMethodHandler, nameof(invokeMethodHandler));
            this.subscriptionsToProcess = new ConcurrentDictionary<string, ConcurrentQueue<(DeviceSubscription, bool)>>();
            this.processSubscriptionsBlock = new ActionBlock<string>(this.ProcessSubscriptions);
        }

        public Task AddSubscription(string id, DeviceSubscription deviceSubscription)
        {
            this.connectionManager.AddSubscription(id, deviceSubscription);
            this.AddSubscriptionsToProcess(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, true) });
            return Task.CompletedTask;
        }

        public Task RemoveSubscription(string id, DeviceSubscription deviceSubscription)
        {
            this.connectionManager.RemoveSubscription(id, deviceSubscription);
            this.AddSubscriptionsToProcess(id, new List<(DeviceSubscription, bool)> { (deviceSubscription, false) });
            return Task.CompletedTask;
        }

        public Task ProcessSubscriptions(string id, IEnumerable<(DeviceSubscription, bool)> subscriptions)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            List<(DeviceSubscription, bool)> subscriptionsList = Preconditions.CheckNotNull(subscriptions, nameof(subscriptions)).ToList();

            foreach ((DeviceSubscription deviceSubscription, bool addSubscription) in subscriptionsList)
            {
                if (addSubscription)
                {
                    //                    RoutingEdgeHub.Events.AddingSubscription(id, deviceSubscription);
                    this.connectionManager.AddSubscription(id, deviceSubscription);
                }
                else
                {
                    //                    RoutingEdgeHub.Events.RemovingSubscription(id, deviceSubscription);
                    this.connectionManager.RemoveSubscription(id, deviceSubscription);
                }
            }

            this.AddSubscriptionsToProcess(id, subscriptionsList);
            return Task.CompletedTask;
        }

        internal async Task ProcessSubscription(string id, Option<ICloudProxy> cloudProxy, DeviceSubscription deviceSubscription, bool addSubscription)
        {
            //RoutingEdgeHub.Events.ProcessingSubscription(id, deviceSubscription);
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

        async Task ProcessSubscriptions(string id)
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

        void AddSubscriptionsToProcess(string id, List<(DeviceSubscription, bool)> subscriptions)
        {
            ConcurrentQueue<(DeviceSubscription, bool)> clientSubscriptionsQueue = this.GetClientSubscriptionsQueue(id);
            subscriptions.ForEach(s => clientSubscriptionsQueue.Enqueue(s));
            this.processSubscriptionsBlock.Post(id);
        }

        ConcurrentQueue<(DeviceSubscription, bool)> GetClientSubscriptionsQueue(string id)
            => this.subscriptionsToProcess.GetOrAdd(id, new ConcurrentQueue<(DeviceSubscription, bool)>());
    }
}
