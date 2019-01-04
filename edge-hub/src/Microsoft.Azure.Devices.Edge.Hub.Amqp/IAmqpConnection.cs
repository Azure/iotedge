// Copyright (c) Microsoft. All rights reserved.
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

        T FindExtension<T>();

        Task Close();
    }
}
