// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class TokenCredentials : ITokenCredentials
    {
        public TokenCredentials(IIdentity identity, string token, string productInfo, Option<string> modelId, bool updatable)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.Token = Preconditions.CheckNonWhiteSpace(token, nameof(token));
            this.AuthenticationType = AuthenticationType.Token;
            this.ProductInfo = productInfo ?? string.Empty;
            this.ModelId = modelId;
            this.IsUpdatable = updatable;
        }

        public IIdentity Identity { get; }

        public string Token { get; }

        public bool IsUpdatable { get; }

        public AuthenticationType AuthenticationType { get; }

        public string ProductInfo { get; }

        public Option<string> ModelId { get; set; }
    }
}
