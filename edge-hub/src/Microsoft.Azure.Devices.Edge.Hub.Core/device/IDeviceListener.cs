// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IDeviceListener
    {
        Task ReceiveMessage(IMessage message);

        Task ReceiveMessageBatch(IEnumerable<IMessage> message);

        Task UpdateReportedProperties(TwinCollection reportedProperties);

        Task<Twin> GetTwin();

        Task<object> CallMethod(string methodName, object parameters, string deviceId);

        void BindDeviceProxy(IDeviceProxy deviceProxy);

        Task CloseAsync();

        Task ReceiveFeedbackMessage(IFeedbackMessage feedbackMessage);

        IIdentity Identity { get; }
    }
}
