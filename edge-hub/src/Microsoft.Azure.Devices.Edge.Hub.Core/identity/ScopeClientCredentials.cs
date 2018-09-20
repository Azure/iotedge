// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ScopeClientCredentials : IClientCredentials
    {
        public ScopeClientCredentials(ServiceIdentity serviceIdentity, IIdentity identity, string productInfo)
        {
            this.ServiceIdentity = Preconditions.CheckNotNull(serviceIdentity, nameof(serviceIdentity));
            this.AuthenticationType = AuthenticationType.Scope;
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.ProductInfo = productInfo;
        }

        public ServiceIdentity ServiceIdentity { get; }

        public IIdentity Identity { get; }

        public AuthenticationType AuthenticationType { get; }

        public string ProductInfo { get; }
    }
}
