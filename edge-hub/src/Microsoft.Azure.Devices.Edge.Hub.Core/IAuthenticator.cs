// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public interface IAuthenticator
    {
        bool Authenticate(string connectionString);
    }
}
