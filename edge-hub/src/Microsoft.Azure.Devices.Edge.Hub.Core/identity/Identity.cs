// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;

    public abstract class Identity : IIdentity
    {
        protected Identity(
            string iotHubHostName,
            string connectionString,
            AuthenticationScope scope,
            string policyName,
            string secret,
            string productInfo,
            Option<string> token)
        {
            this.IotHubHostName = iotHubHostName;
            this.ConnectionString = connectionString;
            this.Scope = scope;
            this.PolicyName = policyName;
            this.Secret = secret;
            this.ProductInfo = productInfo;
            this.Token = token;
        }

        protected Identity(
            string iotHubHostName,
            AuthenticationScope scope,
            string productInfo)
        {
            this.IotHubHostName = iotHubHostName;
            this.ConnectionString = null;
            this.Scope = scope;
            this.PolicyName = null;
            this.Secret = null;
            this.ProductInfo = productInfo;
            this.Token = Option.None<string>();
        }

        public string IotHubHostName { get; }

        public string ConnectionString { get; }

        public Option<string> Token { get; }

        public abstract string Id { get; }

        public AuthenticationScope Scope { get; }

        public string PolicyName { get; }

        public string Secret { get; }

        public string ProductInfo { get; }
    }
}
