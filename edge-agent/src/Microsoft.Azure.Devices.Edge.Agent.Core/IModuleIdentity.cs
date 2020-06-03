// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IModuleIdentity
    {
        string IotHubHostname { get; }

        string DeviceId { get; }

        string ModuleId { get; }

        ICredentials Credentials { get; }
    }
}
