// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System.Threading.Tasks;

    public interface IDeviceProxy
    {
        Task<bool> Disconnect();
        Task SendMessage(IMessage message);
        Task<object> CallMethod(string method, object parameters);
    }
}
