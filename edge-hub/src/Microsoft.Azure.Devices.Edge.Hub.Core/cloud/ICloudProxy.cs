// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ICloudProxy
    {
        Task<bool> CloseAsync();

        Task<bool> SendMessage(IMessage message);

        Task<bool> SendMessageBatch(IEnumerable<IMessage> inputMessages);

        Task UpdateReportedProperties(TwinCollection reportedProperties);

        Task<Twin> GetTwin();

        void BindCloudListener(ICloudListener cloudListener);

        bool IsActive { get; }

        Task SendFeedbackMessage(IFeedbackMessage message);
    }
}
