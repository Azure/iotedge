// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface IIdentityProvider
    {
        IIdentity Create(string id);

        IIdentity Create(string deviceId, string moduleId);
    }
}
