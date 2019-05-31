// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IModuleConnection
    {
        Task<IModuleClient> GetOrCreateModuleClient();

        Option<IModuleClient> GetModuleClient();
    }
}
