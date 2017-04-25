// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ICloudProxy
    {
        Task<bool> Disconnect();
        Task<bool> SendMessage(IMessage message);
        Task<bool> SendMessageBatch(IEnumerable<IMessage> inputMessages);
        Task UpdateReportedProperties(TwinCollection reportedProperties);
        Task<Twin> GetTwin();
    }
}
