// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IDeviceListener
    {
        Task ReceiveMessage(IMessage message);
        Task ReceiveMessageBatch(IEnumerable<IMessage> message);
        Task UpdateReportedProperties(TwinCollection reportedProperties, string deviceId);
        Task<Twin> GetTwin(string deviceId);
        Task<object> CallMethod(string methodName, object parameters, string deviceId);
    }
}
