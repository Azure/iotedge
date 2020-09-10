// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IClientCredentials
    {
        IIdentity Identity { get; }

        AuthenticationType AuthenticationType { get; }

        string ProductInfo { get; }

        Option<string> ModelId { get; set; }
    }
}
