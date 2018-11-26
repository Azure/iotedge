// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface IIdentityProvider
    {
        IIdentity Create(string id);

        IIdentity Create(string deviceId, string moduleId);
    }
}
