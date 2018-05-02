// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public class ConnectionStringCredentials : ICredentials
    {
        public ConnectionStringCredentials(string connectionString)
        {
            this.CredentialsType = CredentialType.ConnectionString;
            this.ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public CredentialType CredentialsType { get; }
    }
}
