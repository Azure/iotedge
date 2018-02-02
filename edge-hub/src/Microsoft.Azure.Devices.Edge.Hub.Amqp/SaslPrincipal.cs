namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Security.Principal;
    using Microsoft.Azure.Devices.Edge.Util;
    using IEdgeHubIdentity = Microsoft.Azure.Devices.Edge.Hub.Core.Identity.IIdentity;

    public class SaslPrincipal : IPrincipal
    {
        public SaslPrincipal(SaslIdentity saslIdentity, IEdgeHubIdentity edgeHubIdentity)
        {
            this.SaslIdentity = Preconditions.CheckNotNull(saslIdentity, nameof(saslIdentity));
            this.EdgeHubIdentity = Preconditions.CheckNotNull(edgeHubIdentity, nameof(edgeHubIdentity));
        }

        // TODO: Remove resharper suppressions file once implementation is complete.
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        // ReSharper disable once MemberCanBePrivate.Global
        public SaslIdentity SaslIdentity { get; }

        // TODO: Remove resharper suppressions file once implementation is complete.
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        // ReSharper disable once MemberCanBePrivate.Global
        public IEdgeHubIdentity EdgeHubIdentity { get; }

        public IIdentity Identity => new GenericIdentity(this.EdgeHubIdentity.Id);

        public bool IsInRole(string role) => throw new InvalidOperationException("IsInRole");
    }
}
