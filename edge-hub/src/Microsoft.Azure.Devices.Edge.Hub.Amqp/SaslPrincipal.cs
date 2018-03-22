// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Security.Principal;
    using Microsoft.Azure.Devices.Edge.Util;

    class SaslPrincipal : IPrincipal
    {
        public SaslPrincipal(AmqpAuthentication amqpAuthentication)
        {
            this.AmqpAuthentication = Preconditions.CheckNotNull(amqpAuthentication, nameof(amqpAuthentication));
            this.Identity = new GenericIdentity(amqpAuthentication.ClientCredentials.Map(i => i.Identity.Id).GetOrElse(string.Empty));
        }

        public AmqpAuthentication AmqpAuthentication { get; }

        public IIdentity Identity { get; }

        public bool IsInRole(string role) => throw new NotImplementedException();
    }
}
