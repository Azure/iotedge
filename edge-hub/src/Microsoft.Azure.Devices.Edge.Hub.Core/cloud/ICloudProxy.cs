// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Shared;

    public interface ICloudProxy
    {
        Task<bool> CloseAsync();

        Task<bool> SendMessageAsync(IMessage message);

        Task<bool> SendMessageBatchAsync(IEnumerable<IMessage> inputMessages);

        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties);

        Task<IMessage> GetTwinAsync();

        void BindCloudListener(ICloudListener cloudListener);

        bool IsActive { get; }

        Task SendFeedbackMessageAsync(IFeedbackMessage message);
    }
}
