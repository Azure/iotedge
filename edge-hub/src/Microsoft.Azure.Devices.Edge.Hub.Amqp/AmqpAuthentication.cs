// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AmqpAuthentication
    {
        public static AmqpAuthentication Unauthenticated = new AmqpAuthentication(false, Option.None<IClientCredentials>());

        public AmqpAuthentication(bool isAuthenticated, Option<IClientCredentials> clientCredentials)
        {
            this.IsAuthenticated = isAuthenticated;
            this.ClientCredentials = clientCredentials;
        }

        public bool IsAuthenticated { get; }

        public Option<IClientCredentials> ClientCredentials { get; }
    }
}
