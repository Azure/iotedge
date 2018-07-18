// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    public interface IInvokeMethodHandler
    {
        Task<DirectMethodResponse> InvokeMethod(DirectMethodRequest request);

        Task ProcessInvokeMethodSubscription(string id);
    }
}
