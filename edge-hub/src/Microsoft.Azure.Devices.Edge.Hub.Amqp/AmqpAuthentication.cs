// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AmqpAuthentication
    {
        public static readonly AmqpAuthentication Unauthenticated = new AmqpAuthentication(false, Option.None<IClientCredentials>());

        public AmqpAuthentication(bool isAuthenticated, Option<IClientCredentials> clientCredentials)
        {
            this.IsAuthenticated = isAuthenticated;
            this.ClientCredentials = clientCredentials;
        }

        public Option<IClientCredentials> ClientCredentials { get; }

        public bool IsAuthenticated { get; }
    }
}
