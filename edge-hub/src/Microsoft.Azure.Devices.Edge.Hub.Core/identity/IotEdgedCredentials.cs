// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        public AuthenticationType AuthenticationType { get; }

        public IIdentity Identity { get; }

        public string ProductInfo { get; }
    }
}
