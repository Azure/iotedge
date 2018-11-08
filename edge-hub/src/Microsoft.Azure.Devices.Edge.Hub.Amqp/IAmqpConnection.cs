// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Security.Principal;
    using System.Threading.Tasks;

    /// <summary>
    /// This interface contains functionality similar to AmqpConnection.
    /// This allows unit testing the components that use it
    /// </summary>
    public interface IAmqpConnection
    {
        IPrincipal Principal { get; }

        Task Close();

        T FindExtension<T>();
    }
}
