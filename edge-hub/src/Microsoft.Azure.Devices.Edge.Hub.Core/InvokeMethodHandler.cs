// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class InvokeMethodHandler : IInvokeMethodHandler
    {
        readonly ConcurrentDictionary<string, ConcurrentDictionary<DirectMethodRequest, TaskCompletionSource<DirectMethodResponse>>> clientMethodRequestQueue = new ConcurrentDictionary<string, ConcurrentDictionary<DirectMethodRequest, TaskCompletionSource<DirectMethodResponse>>>();
        readonly IConnectionManager connectionManager;

        public InvokeMethodHandler(IConnectionManager connectionManager)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
        }

        public Task<DirectMethodResponse> InvokeMethod(DirectMethodRequest methodRequest)
        {
            Preconditions.CheckNotNull(methodRequest, nameof(methodRequest));

            Option<IDeviceProxy> deviceProxy = this.GetDeviceProxyWithSubscription(methodRequest.Id);
            return deviceProxy.Map(
                    d =>
                    {
                        Events.InvokingMethod(methodRequest);
                        return d.InvokeMethodAsync(methodRequest);
                    })
                .GetOrElse(
                    async () =>
                    {
                        var taskCompletion = new TaskCompletionSource<DirectMethodResponse>();
                        ConcurrentDictionary<DirectMethodRequest, TaskCompletionSource<DirectMethodResponse>> clientQueue = this.GetClientQueue(methodRequest.Id);
                        clientQueue.TryAdd(methodRequest, taskCompletion);
                        Task completedTask = await Task.WhenAny(taskCompletion.Task, Task.Delay(methodRequest.ConnectTimeout));
                        if (completedTask != taskCompletion.Task && clientQueue.TryRemove(methodRequest, out taskCompletion))
                        {
                            taskCompletion.TrySetResult(new DirectMethodResponse(new EdgeHubTimeoutException($"Client {methodRequest.Id} not found"), HttpStatusCode.NotFound));
                        }

                        return await taskCompletion.Task;
                    });
        }

        public Task ProcessInvokeMethodSubscription(string id)
        {
            this.ProcessInvokeMethodSubscriptionInternal(id);
            return Task.CompletedTask;
        }

        async void ProcessInvokeMethodSubscriptionInternal(string id)
        {
            try
            {
                Events.ProcessingInvokeMethodQueue(id);
                // Temporary hack to wait for the subscription call to complete. Without this,
                // the EdgeHub will invoke the pending method request "too soon", before the layers
                // in between have been set up correctly. To fix this, changes are needed in ProtocolGateway,
                // Client SDK, and need to figure out a way to raise events for AMQP Links. 
                await Task.Delay(TimeSpan.FromSeconds(3));
                await this.ProcessInvokeMethodsForClient(id);
            }
            catch (Exception e)
            {
                Events.ErrorProcessingInvokeMethodRequests(e, id);
            }
        }

        Option<IDeviceProxy> GetDeviceProxyWithSubscription(string id)
        {
            Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(id);
            if (!deviceProxy.HasValue)
            {
                Events.NoDeviceProxyForMethodInvocation(id);
            }
            else if (!this.connectionManager.GetSubscriptions(id)
                .Filter(s => s.TryGetValue(DeviceSubscription.Methods, out bool isActive) && isActive)
                .HasValue)
            {
                Events.NoSubscriptionForMethodInvocation(id);
            }
            else
            {
                return deviceProxy;
            }
            return Option.None<IDeviceProxy>();
        }

        async Task ProcessInvokeMethodsForClient(string id)
        {
            Option<IDeviceProxy> deviceProxy = this.GetDeviceProxyWithSubscription(id);
            await deviceProxy.ForEachAsync(
                async dp =>
                {
                    ConcurrentDictionary<DirectMethodRequest, TaskCompletionSource<DirectMethodResponse>> clientQueue = this.GetClientQueue(id);
                    foreach (KeyValuePair<DirectMethodRequest, TaskCompletionSource<DirectMethodResponse>> invokeMethodRequest in clientQueue)
                    {
                        if (clientQueue.TryRemove(invokeMethodRequest.Key, out TaskCompletionSource<DirectMethodResponse> taskCompletionSource))
                        {
                            DirectMethodResponse response = await dp.InvokeMethodAsync(invokeMethodRequest.Key);
                            taskCompletionSource.TrySetResult(response);
                        }
                    }
                });
        }

        ConcurrentDictionary<DirectMethodRequest, TaskCompletionSource<DirectMethodResponse>> GetClientQueue(string id) => this.clientMethodRequestQueue.GetOrAdd(
            Preconditions.CheckNonWhiteSpace(id, nameof(id)),
            new ConcurrentDictionary<DirectMethodRequest, TaskCompletionSource<DirectMethodResponse>>());

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<InvokeMethodHandler>();
            const int IdStart = HubCoreEventIds.InvokeMethodHandler;

            enum EventIds
            {
                InvokingMethod = IdStart,
                NoSubscription,
                ClientNotFound,
                ProcessingInvokeMethodQueue,
                ErrorProcessingInvokeMethodRequests
            }

            public static void InvokingMethod(DirectMethodRequest methodRequest)
            {
                Log.LogDebug((int)EventIds.InvokingMethod, Invariant($"Invoking method {methodRequest.Name} on client {methodRequest.Id}."));
            }

            public static void NoSubscriptionForMethodInvocation(string id)
            {
                Log.LogWarning((int)EventIds.NoSubscription, Invariant($"Unable to invoke method on client {id} because no subscription for methods for found."));
            }

            public static void NoDeviceProxyForMethodInvocation(string id)
            {
                Log.LogWarning((int)EventIds.ClientNotFound, Invariant($"Unable to invoke method as client {id} is not connected."));
            }

            public static void ProcessingInvokeMethodQueue(string id)
            {
                Log.LogDebug((int)EventIds.ProcessingInvokeMethodQueue, Invariant($"Processing pending method invoke requests for client {id}."));
            }

            public static void ErrorProcessingInvokeMethodRequests(Exception exception, string id)
            {
                Log.LogWarning((int)EventIds.ErrorProcessingInvokeMethodRequests, exception, Invariant($"Error processing invoke method requests for client {id}."));
            }
        }
    }
}
