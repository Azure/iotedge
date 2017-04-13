// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;

    public interface ICloudProxy
    {
        Task<bool> Disconnect();
        Task SendMessage(IMessage message);
        Task SendFeedback(string lockToken, FeedbackStatus status);
        Task UpdateReportedProperties(TwinCollection reportedProperties);
        Task<Twin> GetTwin();
    }
}
