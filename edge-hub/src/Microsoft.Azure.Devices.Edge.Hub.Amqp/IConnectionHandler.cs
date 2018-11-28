// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface IConnectionHandler
    {
        Task<IDeviceListener> GetDeviceListener();

        //Task<AmqpAuthentication> GetAmqpAuthentication();

        //IDeviceListener DeviceListener { get; }

        //IAmqpAuthenticator AmqpAuthenticator { get; }

        Task RegisterLinkHandler(ILinkHandler linkHandler);

        Task RemoveLinkHandler(ILinkHandler linkHandler);
    }
}
