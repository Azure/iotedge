// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    class Authenticator : IAuthenticator
    {
        readonly IConnectionManager connectionManager;

        public Authenticator(IConnectionManager connectionManager)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);
        }

        public bool Authenticate(string connectionString)
        {
            throw new NotImplementedException();
        }
    }
}
