// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public interface IEnvironmentProvider
    {
        IEnvironment Create(DeploymentConfig deploymentConfigInfo);
    }
}
