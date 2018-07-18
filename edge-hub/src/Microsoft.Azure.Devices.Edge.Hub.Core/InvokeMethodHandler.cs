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

        public async Task<DirectMethodResponse> InvokeMethod(DirectMethodRequest methodRequest)
        {
            Preconditions.CheckNotNull(methodRequest, nameof(methodRequest));
            Events.InvokingMethod(methodRequest);
            var taskCompletion = new TaskCompletionSource<DirectMethodResponse>();
            ConcurrentDictionary<DirectMethodRequest, TaskCompletionSource<DirectMethodResponse>> clientQueue = this.GetClientQueue(methodRequest.Id);
            clientQueue.TryAdd(methodRequest, taskCompletion);
            await this.ProcessInvokeMethodsForClient(methodRequest.Id);
            Task completedTask = await Task.WhenAny(taskCompletion.Task, Task.Delay(methodRequest.ConnectTimeout));
            if (completedTask != taskCompletion.Task && clientQueue.TryRemove(methodRequest, out taskCompletion))
            {
                taskCompletion.TrySetResult(new DirectMethodResponse(new InvalidOperationException($"Client {methodRequest.Id} not found"), HttpStatusCode.NotFound));
            }

            return await taskCompletion.Task;
        }

        public Task ProcessInvokeMethodSubscription(string id)
        {
            // Call the method to handle invoke method requests, but don't block on that call,
            // since the subscription call must complete before the invoke method is called.
            this.ProcessInvokeMethodsSubscriptionInternal(id);
            return Task.CompletedTask;            
        }

        async void ProcessInvokeMethodsSubscriptionInternal(string id)
        {
            try
            {
                Events.ProcessingInvokeMethodQueue(id);
                // Wait for the Subscription call to complete successfully
                await Task.Delay(TimeSpan.FromSeconds(2));
                await this.ProcessInvokeMethodsForClient(Preconditions.CheckNonWhiteSpace(id, nameof(id)));
            }
            catch (Exception e)
            {
                Events.ErrorProcessingInvokeMethodRequests(e, id);
            }
        }

        async Task ProcessInvokeMethodsForClient(string id)
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
