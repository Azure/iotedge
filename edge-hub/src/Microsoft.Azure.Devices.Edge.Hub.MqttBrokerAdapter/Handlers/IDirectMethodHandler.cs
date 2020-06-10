// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IDirectMethodHandler
    {
        Task<DirectMethodResponse> CallDirectMethodAsync(DirectMethodRequest request, IIdentity identity);
    }
}
