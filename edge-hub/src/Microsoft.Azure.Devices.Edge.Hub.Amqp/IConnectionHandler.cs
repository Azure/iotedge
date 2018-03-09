// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface IConnectionHandler
    {
        Task<IDeviceListener> GetDeviceListener();

        Task<AmqpAuthentication> GetAmqpAuthentication();

        void RegisterC2DMessageSender(Func<IMessage, Task> func);

        void RegisterModuleMessageSender(Func<string, IMessage, Task> func);

        void RegisterMethodInvoker(Func<DirectMethodRequest, Task> func);

        void RegisterDesiredPropertiesUpdateSender(Func<IMessage, Task> func);
    }
}
