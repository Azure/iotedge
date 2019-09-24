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
        public KubernetesModuleIdentityLifecycleManager(IIdentityManager identityManager, ModuleIdentityProviderServiceBuilder identityProviderServiceBuilder, Uri workloadUri)
            : base(identityManager, identityProviderServiceBuilder, workloadUri)
        {
        }

        protected override async Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesWorkAsync(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);

            IImmutableDictionary<string, IModuleIdentity> moduleIdentities = await this.GetModuleIdentitiesAsync(diff);
            return moduleIdentities;
        }
    }
}
