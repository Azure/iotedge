// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AmqpAuthentication
    {
        public static AmqpAuthentication Unauthenticated = new AmqpAuthentication(false, Option.None<IIdentity>());

        public AmqpAuthentication(bool isAuthenticated, Option<IIdentity> identity)
        {
            this.IsAuthenticated = isAuthenticated;
            this.Identity = identity;
        }

        public bool IsAuthenticated { get; }

        public Option<IIdentity> Identity { get; }
    }
}
