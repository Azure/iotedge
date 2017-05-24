// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ICloudProxy
    {
        Task<bool> CloseAsync();

        Task<bool> SendMessageAsync(IMessage message);

        Task<bool> SendMessageBatchAsync(IEnumerable<IMessage> inputMessages);

        Task UpdateReportedPropertiesAsync(string reportedProperties);

        Task<IMessage> GetTwinAsync();

        void BindCloudListener(ICloudListener cloudListener);

        bool IsActive { get; }

        Task SendFeedbackMessageAsync(IFeedbackMessage message);

        Task SetupCallMethodAsync();

        Task RemoveCallMethodAsync();
    }
}
