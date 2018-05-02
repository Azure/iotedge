// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public interface IModuleIdentity
    {
        string IotHubHostname { get; }

        string GatewayHostname { get; }

        string DeviceId { get; }

        string ModuleId { get; }

        ICredentials Credentials { get; }
    }
}
