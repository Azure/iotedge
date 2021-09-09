// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;

    public interface ICloudListener
    {
        Task ProcessMessageAsync(IMessage message);

        Task OnDesiredPropertyUpdates(IMessage desiredProperties);

        Task<DirectMethodResponse> CallMethodAsync(DirectMethodRequest request);
    }
}
