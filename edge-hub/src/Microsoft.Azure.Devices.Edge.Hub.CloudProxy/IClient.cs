// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        Task AbandonAsync(string messageId);

        Task CloseAsync();

        Task CompleteAsync(string messageId);

        Task<Twin> GetTwinAsync();

        Task OpenAsync();

        Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout);

        Task RejectAsync(string messageId);

        Task SendEventAsync(Message message);

        Task SendEventBatchAsync(IEnumerable<Message> messages);

        void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler handler);

        Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyUpdates1, object userContext);

        Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext);

        void SetOperationTimeoutInMilliseconds(uint defaultOperationTimeoutMilliseconds);

        void SetProductInfo(string productInfo);

        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties);
    }
}
