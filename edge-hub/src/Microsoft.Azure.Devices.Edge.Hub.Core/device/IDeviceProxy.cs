// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// The <c>IDeviceProxy</c> represents a connection to a device that is connected
    /// to the edge hub. Objects implementing this interface are used to send messages to
    /// and call methods on the device. There is exactly one instance of a device proxy
    /// object for each device that is connected to the edge hub. The <see cref="IConnectionManager"/>
    /// object is responsible for creating and maintaining instances of <c>IDeviceProxy</c>
    /// for every connecting device.
    /// In the MQTT implementation for example the implementation of this interface uses
    /// the protocol gateway library to interface with MQTT clients by transforming messages
    /// between MQTT packets and <see cref="IMessage"/> objects.
    /// </summary>
    public interface IDeviceProxy
    {
        IIdentity Identity { get; }

        bool IsActive { get; }

        Task CloseAsync(Exception ex);

        Task<Option<IClientCredentials>> GetUpdatedIdentity();

        Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request);

        Task OnDesiredPropertyUpdates(IMessage desiredProperties);

        Task SendC2DMessageAsync(IMessage message);

        Task SendMessageAsync(IMessage message, string input);

        Task SendTwinUpdate(IMessage twin);

        void SetInactive();
    }
}
