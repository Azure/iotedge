namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Security.Principal;
    using Microsoft.Azure.Devices.Edge.Util;

    class SaslPrincipal : IPrincipal
    {
        public SaslPrincipal(SaslIdentity saslIdentity, AmqpAuthentication amqpAuthentication)
        {
            this.SaslIdentity = Preconditions.CheckNotNull(saslIdentity, nameof(saslIdentity));
            this.AmqpAuthentication = Preconditions.CheckNotNull(amqpAuthentication, nameof(amqpAuthentication));
            this.Identity = new GenericIdentity(amqpAuthentication.Identity.Map(i => i.Id).GetOrElse(string.Empty));
        }

        // TODO: Remove resharper suppressions file once implementation is complete.
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        // ReSharper disable once MemberCanBePrivate.Global
        public SaslIdentity SaslIdentity { get; }

        public AmqpAuthentication AmqpAuthentication { get; }

        public IIdentity Identity { get; }

        public bool IsInRole(string role) => throw new NotImplementedException();
    }
}
