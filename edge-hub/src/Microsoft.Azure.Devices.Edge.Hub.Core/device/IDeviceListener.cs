// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IDeviceListener
    {
        Task ProcessMessageAsync(IMessage message);

        Task ProcessMessageBatchAsync(IEnumerable<IMessage> message);

        Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage);

        Task<IMessage> GetTwinAsync();

        Task ProcessMethodResponseAsync(DirectMethodResponse response);

        void BindDeviceProxy(IDeviceProxy deviceProxy);

        Task CloseAsync();

        Task ProcessFeedbackMessageAsync(IFeedbackMessage feedbackMessage);

        IIdentity Identity { get; }
    }
}
