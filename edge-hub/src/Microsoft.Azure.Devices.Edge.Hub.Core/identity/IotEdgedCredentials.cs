// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class IotEdgedCredentials : IClientCredentials
    {
        public IotEdgedCredentials(IIdentity identity, string productInfo)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.ProductInfo = productInfo ?? string.Empty;
            this.AuthenticationType = AuthenticationType.IoTEdged;
        }

        public IIdentity Identity { get; }

        public AuthenticationType AuthenticationType { get; }

        public string ProductInfo { get; }
    }
}
