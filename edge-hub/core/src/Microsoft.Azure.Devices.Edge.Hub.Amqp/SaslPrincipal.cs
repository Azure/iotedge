// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using IIdentity = System.Security.Principal.IIdentity;

    class SaslPrincipal : IPrincipal, IAmqpAuthenticator
    {
        readonly IClientCredentials clientCredentials;
        readonly bool isAuthenticated;

        public SaslPrincipal(bool isAuthenticated, IClientCredentials clientCredentials)
        {
            this.isAuthenticated = isAuthenticated;
            this.clientCredentials = Preconditions.CheckNotNull(clientCredentials, nameof(clientCredentials));
            this.Identity = new GenericIdentity(this.clientCredentials.Identity.Id);
        }

        public IIdentity Identity { get; }

        public bool IsInRole(string role) => throw new NotImplementedException();

        public Task<bool> AuthenticateAsync(string id) =>
            Task.FromResult(
                this.isAuthenticated &&
                this.clientCredentials.Identity.Id.Equals(id));
    }
}
