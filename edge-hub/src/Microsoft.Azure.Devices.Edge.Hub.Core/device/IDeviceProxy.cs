// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Threading.Tasks;

    public interface IDeviceProxy
    {
        Task CloseAsync(Exception ex);

        Task<bool> SendMessageAsync(IMessage message);

        Task<bool> SendMessage(IMessage message, string endpoint);

        Task<object> CallMethodAsync(string method, object parameters);

        bool IsActive { get; }

        IIdentity Identity { get; }

        void SetInactive();
    }
}
