// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class SharedKeyCredentials : ISharedKeyCredentials
    {
        public SharedKeyCredentials(IIdentity identity, string connectionString, string productInfo)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.ConnectionString = Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            this.AuthenticationType = AuthenticationType.SasKey;
            this.ProductInfo = productInfo ?? string.Empty;
        }

        public IIdentity Identity { get; }

        public string ConnectionString { get; }

        public AuthenticationType AuthenticationType { get; }

        public string ProductInfo { get; }
    }
}
