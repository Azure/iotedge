// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;

    public interface ICloudListener
    {
        Task ReceiveMessage(IMessage message);
        Task<object> CallMethod(string methodName, object parameters, string deviceId);
    }
}
