// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Threading.Tasks;

    public interface ICloudListener
    {
        Task ProcessMessageAsync(IMessage message);

        Task<object> CallMethodAsync(string methodName, object parameters, string deviceId);
    }
}
