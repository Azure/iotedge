// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using Autofac;

    public interface IDependencyManager
    {
        void Register(ContainerBuilder builder);
    }
}
