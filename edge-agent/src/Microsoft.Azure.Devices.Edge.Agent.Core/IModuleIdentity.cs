// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Newtonsoft.Json;

    public interface IModuleIdentity
    {
        string IotHubHostname { get; }

        string GatewayHostname { get; }

        string DeviceId { get; }

        string ModuleId { get; }

        ICredentials Credentials { get; }
    }
}
