// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IDeviceListener
    {
        Task ProcessDeviceMessageAsync(IMessage message);

        Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> message);

        Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage);

        Task<IMessage> GetTwinAsync();

        Task ProcessMethodResponseAsync(IMessage message);

        void BindDeviceProxy(IDeviceProxy deviceProxy);

        Task CloseAsync();

        Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus);

        IIdentity Identity { get; }
    }
}
