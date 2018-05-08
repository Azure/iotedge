// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    public interface IClient : IDisposable
    {
        bool IsActive { get; }

        Task<Twin> GetTwinAsync();

        Task SendEventAsync(Message message);

        Task SendEventBatchAsync(IEnumerable<Message> messages);

        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties);

        Task CompleteAsync(string messageId);

        Task AbandonAsync(string messageId);

        Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext);

        Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyUpdates1, object userContext);

        void SetOperationTimeoutInMilliseconds(uint defaultOperationTimeoutMilliseconds);

        void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler handler);

        void SetProductInfo(string productInfo);

        Task OpenAsync();

        Task CloseAsync();        
    }
}
