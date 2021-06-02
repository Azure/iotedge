// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class IotEdgedCredentials : IClientCredentials
    {
        public IotEdgedCredentials(IIdentity identity, string productInfo, Option<string> modelId, Option<string> authChain)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.ProductInfo = productInfo ?? string.Empty;
            this.ModelId = modelId;
            this.AuthChain = authChain;
            this.AuthenticationType = AuthenticationType.IoTEdged;
        }

        public IIdentity Identity { get; }

        public AuthenticationType AuthenticationType { get; }

        public string ProductInfo { get; }

        public Option<string> ModelId { get; }

        public Option<string> AuthChain { get; }
    }
}
