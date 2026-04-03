// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public interface IClient : IAsyncDisposable, IDisposable
    {
        bool IsActive { get; }

        Task<TwinProperties> GetTwinPropertiesAsync();

        Task SendTelemetryAsync(TelemetryMessage message);

        Task SendTelemetryAsync(IEnumerable<TelemetryMessage> messages);

        Task UpdateReportedPropertiesAsync(PropertyCollection reportedProperties);

        Task CompleteAsync(string messageId);

        Task AbandonAsync(string messageId);

        Task SetDirectMethodCallbackAsync(Func<Client.DirectMethodRequest, Task<Client.DirectMethodResponse>> methodHandler);

        Task SetDesiredPropertyUpdateCallbackAsync(Func<PropertyCollection, Task> onDesiredPropertyUpdates);

        void SetConnectionStatusChangedHandler(Action<ConnectionStatusInfo> handler);

        void SetProductInfo(string productInfo);

        Task OpenAsync();

        Task CloseAsync();

        Task RejectAsync(string messageId);

        Task<IncomingMessage> ReceiveAsync(TimeSpan receiveMessageTimeout);
    }
}
