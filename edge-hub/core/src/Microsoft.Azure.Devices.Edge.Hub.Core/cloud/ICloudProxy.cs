// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// The <c>ICloudProxy</c> represents the cloud-side of a device that is
    /// connected to the edge hub. Objects implementing this interface essentially
    /// use the IoT Hub Module Client/Device Client to open and maintain a connection to the
    /// module’s counterpart in Azure IoT Hub.
    /// There is exactly one instance of a cloud proxy object for each device that
    /// is connected to the edge hub. The <see cref="IConnectionManager"/>
    /// object is responsible for creating and maintaining instances of <c>ICloudProxy</c>
    /// for every connecting device.
    /// </summary>
    public interface ICloudProxy
    {
        bool IsActive { get; }

        Task<bool> CloseAsync();

        Task<bool> OpenAsync();

        Task SendMessageAsync(IMessage message);

        Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages);

        Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage);

        Task<IMessage> GetTwinAsync();

        Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus);

        Task SetupCallMethodAsync();

        Task RemoveCallMethodAsync();

        Task SetupDesiredPropertyUpdatesAsync();

        Task RemoveDesiredPropertyUpdatesAsync();

        Task StartListening();
    }
}
