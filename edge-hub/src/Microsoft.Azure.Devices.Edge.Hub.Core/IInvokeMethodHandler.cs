// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
        /// <param name="id">Invoke method Id</param>
        /// <returns>Task for invoke method subscription</returns>
        Task ProcessInvokeMethodSubscription(string id);
    }
}
