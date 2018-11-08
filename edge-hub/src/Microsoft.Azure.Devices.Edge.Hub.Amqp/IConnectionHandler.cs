// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface IConnectionHandler
    {
        Task<AmqpAuthentication> GetAmqpAuthentication();

        Task<IDeviceListener> GetDeviceListener();

        Task RegisterLinkHandler(ILinkHandler linkHandler);

        Task RemoveLinkHandler(ILinkHandler linkHandler);
    }
}
