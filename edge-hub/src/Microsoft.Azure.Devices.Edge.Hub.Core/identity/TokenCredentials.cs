// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class TokenCredentials : ITokenCredentials
    {
        public TokenCredentials(IIdentity identity, string token, string productInfo)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.Token = Preconditions.CheckNonWhiteSpace(token, nameof(token));
            this.AuthenticationType = AuthenticationType.Token;
            this.ProductInfo = productInfo ?? string.Empty;
        }

        public AuthenticationType AuthenticationType { get; }

        public IIdentity Identity { get; }

        public string ProductInfo { get; }

        public string Token { get; }
    }
}
