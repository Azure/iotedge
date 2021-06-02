// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ImplicitCredentials : IClientCredentials
    {
        public ImplicitCredentials(IIdentity identity, string productInfo, Option<string> modelId)
        {
            this.Identity = identity;
            this.AuthenticationType = AuthenticationType.Implicit;
            this.ProductInfo = productInfo;
            this.ModelId = modelId;
            this.AuthChain = Option.None<string>();
        }

        public IIdentity Identity { get; }

        public AuthenticationType AuthenticationType { get; }

        public string ProductInfo { get; }

        public Option<string> ModelId { get; }

        public Option<string> AuthChain { get; }
    }
}
