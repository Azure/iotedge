// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Security.Principal;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This class wraps an AmqpConnection, and provides similar functionality.
    /// This allows unit testing the components that use it
    /// </summary>
    public class EdgeAmqpConnection : IAmqpConnection
    {
        readonly AmqpConnection underlyingAmqpConnection;

        public EdgeAmqpConnection(AmqpConnection amqpConnection)
        {
            this.underlyingAmqpConnection = Preconditions.CheckNotNull(amqpConnection, nameof(amqpConnection));
        }

        public IPrincipal Principal => this.underlyingAmqpConnection.Principal;

        public T FindExtension<T>() => this.underlyingAmqpConnection.Extensions.Find<T>();

        public Task Close() => this.underlyingAmqpConnection.CloseAsync(this.underlyingAmqpConnection.DefaultCloseTimeout);
    }
}
