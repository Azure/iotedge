// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IClientBuilder
    {
        CloudProxy.IClient Build(IIdentity identity);
    }
}
