// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    public interface IInvokeMethodHandler
    {
        Task<DirectMethodResponse> InvokeMethod(DirectMethodRequest request);

        /// <summary>
        /// This method is called when a client subscribes to Method invocations. 
        /// It processes all the pending method requests for that client (i.e the method requests
        /// that came in before the client subscribed to method invocations and that haven't expired yet)
        /// </summary>
        Task ProcessInvokeMethodSubscription(string id);
    }
}
