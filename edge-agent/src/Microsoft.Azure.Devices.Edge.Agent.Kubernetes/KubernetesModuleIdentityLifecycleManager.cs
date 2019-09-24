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

        public new async Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);
            try
            {
                IImmutableDictionary<string, IModuleIdentity> identities = await this.GetModuleIdentitiesAsync(diff);
                return identities;
            }
            catch (Exception ex)
            {
                Events.ErrorGettingModuleIdentities(ex);
                return ImmutableDictionary<string, IModuleIdentity>.Empty;
            }
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesModuleIdentityLifecycleManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<KubernetesModuleIdentityLifecycleManager>();

            enum EventIds
            {
                ErrorGettingModuleIdentities = IdStart,
            }

            public static void ErrorGettingModuleIdentities(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorGettingModuleIdentities, ex, "Error getting module identities.");
            }
        }
    }
}
