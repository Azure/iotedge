// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;

    public interface IKubernetesSpecFactory<TConfig> 
    {
        IList<KubernetesModule<TConfig>> GetSpec(IList<KubernetesModule<string>> crdSpec);
    }
}