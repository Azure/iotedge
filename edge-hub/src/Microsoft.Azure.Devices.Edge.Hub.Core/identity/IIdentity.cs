// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;

    public interface IIdentity
    {
        string Id { get; }

        string IotHubHostName { get; }

        string ConnectionString { get; }

        Option<string> Token { get; }

        string ProductInfo { get; }

        AuthenticationScope Scope { get; }
    }
}
