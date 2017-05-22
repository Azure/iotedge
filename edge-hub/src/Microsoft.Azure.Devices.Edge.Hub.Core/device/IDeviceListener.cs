// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IDeviceListener
    {
        Task ProcessMessageAsync(IMessage message);

        Task ProcessMessageBatchAsync(IEnumerable<IMessage> message);

        Task UpdateReportedPropertiesAsync(string reportedProperties);

        Task<IMessage> GetTwinAsync();

        Task<object> CallMethodAsync(string methodName, object parameters, string deviceId);

        void BindDeviceProxy(IDeviceProxy deviceProxy);

        Task CloseAsync();

        Task ProcessFeedbackMessageAsync(IFeedbackMessage feedbackMessage);

        IIdentity Identity { get; }
    }
}
