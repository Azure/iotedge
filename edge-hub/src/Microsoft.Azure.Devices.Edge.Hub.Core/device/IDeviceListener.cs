// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IDeviceListener
    {
        IIdentity Identity { get; }

        Task AddDesiredPropertyUpdatesSubscription(string correlationId);

        Task AddSubscription(DeviceSubscription subscription);

        void BindDeviceProxy(IDeviceProxy deviceProxy);

        Task CloseAsync();

        Task ProcessDeviceMessageAsync(IMessage message);

        Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> message);

        Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus);

        Task ProcessMethodResponseAsync(IMessage message);

        Task RemoveDesiredPropertyUpdatesSubscription(string correlationId);

        Task RemoveSubscription(DeviceSubscription subscription);

        Task SendGetTwinRequest(string correlationId);

        Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage, string correlationId);
    }
}
