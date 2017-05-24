// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// The <c>ICloudProxy</c> represents the cloud-side of a device that is
    /// connected to the edge hub. Objects implementing this interface essentially
    /// use the IoT Hub Device SDK to open and maintain a connection to the Azure
    /// IoT Hub device.
    /// 
    /// There is exactly one instance of a cloud proxy object for each device that
    /// is connected to the edge hub. The <see cref="IConnectionManager"/>
    /// object is responsible for creating and maintaining instances of <c>ICloudProxy</c>
    /// for every connecting device.
    /// </summary>
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
