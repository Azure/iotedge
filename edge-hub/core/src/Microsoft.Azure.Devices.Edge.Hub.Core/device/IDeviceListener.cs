// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IDeviceListener
    {
        IIdentity Identity { get; }

        Task ProcessDeviceMessageAsync(IMessage message);

        Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> message);

        Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage, string correlationId);

        Task SendGetTwinRequest(string correlationId);

        Task ProcessMethodResponseAsync(IMessage message);

        void BindDeviceProxy(IDeviceProxy deviceProxy);

        void BindDeviceProxy(IDeviceProxy deviceProxy, Action initWhenBound);

        Task CloseAsync();

        Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus);

        Task AddSubscription(DeviceSubscription subscription);

        Task RemoveSubscription(DeviceSubscription subscription);

        Task RemoveSubscriptions();

        Task AddDesiredPropertyUpdatesSubscription(string correlationId);

        Task RemoveDesiredPropertyUpdatesSubscription(string correlationId);
    }
}
