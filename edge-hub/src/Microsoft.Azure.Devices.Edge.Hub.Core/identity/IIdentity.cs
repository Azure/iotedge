// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface IIdentity
    {
        string Id { get; }

        string IotHubHostName { get; }
    }
}
