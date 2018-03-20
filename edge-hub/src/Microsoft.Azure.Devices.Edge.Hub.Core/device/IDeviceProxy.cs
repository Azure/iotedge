// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    /// <summary>
    /// The <c>IDeviceProxy</c> represents a connection to a device that is connected
    /// to the edge hub. Objects implementing this interface are used to send messages to
    /// and call methods on the device. There is exactly one instance of a device proxy
    /// object for each device that is connected to the edge hub. The <see cref="IConnectionManager"/>
    /// object is responsible for creating and maintaining instances of <c>IDeviceProxy</c>
    /// for every connecting device.
    /// 
    /// In the MQTT implementation for example the implementation of this interface uses
    /// the protocol gateway library to interface with MQTT clients by transforming messages
    /// between MQTT packets and <see cref="IMessage"/> objects.
    /// </summary>
    public interface IDeviceProxy
    {
        Task CloseAsync(Exception ex);

        Task SendC2DMessageAsync(IMessage message);

        Task SendMessageAsync(IMessage message, string input);

        Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request);

        Task OnDesiredPropertyUpdates(IMessage desiredProperties);

        Task SendTwinUpdate(IMessage twin);

        bool IsActive { get; }

        IIdentity Identity { get; }

        void SetInactive();
    }
}
