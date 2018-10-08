// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        Task<IMessage> GetTwinAsync();

        Task<bool> OpenAsync();

        Task RemoveCallMethodAsync();

        Task RemoveDesiredPropertyUpdatesAsync();

        Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus);

        Task SendMessageAsync(IMessage message);

        Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages);

        Task SetupCallMethodAsync();

        Task SetupDesiredPropertyUpdatesAsync();

        void StartListening();

        Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage);
    }
}
