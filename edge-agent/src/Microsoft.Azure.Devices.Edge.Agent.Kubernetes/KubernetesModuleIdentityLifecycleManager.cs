// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class KubernetesModuleIdentityLifecycleManager : Edgelet.ModuleIdentityLifecycleManager
    {
        protected override bool ShouldAlwaysReturnIdentities => true;

        public KubernetesModuleIdentityLifecycleManager(IIdentityManager identityManager, ModuleIdentityProviderServiceBuilder identityProviderServiceBuilder, Uri workloadUri)
            : base(identityManager, identityProviderServiceBuilder, workloadUri)
        {
        }
    }
}
