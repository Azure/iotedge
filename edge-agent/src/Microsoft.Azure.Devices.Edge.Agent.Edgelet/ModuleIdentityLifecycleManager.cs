// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentityLifecycleManager : IModuleIdentityLifecycleManager
    {
        readonly IIdentityManager identityManager;
        readonly ModuleIdentityProviderServiceBuilder identityProviderServiceBuilder;
        readonly Uri workloadUri;

        public ModuleIdentityLifecycleManager(IIdentityManager identityManager, ModuleIdentityProviderServiceBuilder identityProviderServiceBuilder, Uri workloadUri)
        {
            this.identityManager = Preconditions.CheckNotNull(identityManager, nameof(identityManager));
            this.identityProviderServiceBuilder = Preconditions.CheckNotNull(identityProviderServiceBuilder, nameof(identityProviderServiceBuilder));
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
        }

        public async Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);
            if (diff.IsEmpty)
            {
                return ImmutableDictionary<string, IModuleIdentity>.Empty;
            }

            IEnumerable<string> updatedModuleNames = diff.Updated.Select(m => ModuleIdentityHelper.GetModuleIdentityName(m.Name));
            IEnumerable<string> removedModuleNames = diff.Removed.Select(m => ModuleIdentityHelper.GetModuleIdentityName(m));

            IImmutableDictionary<string, Identity> identities = (await this.identityManager.GetIdentities()).ToImmutableDictionary(i => i.ModuleId);

            IEnumerable<string> createIdentities = updatedModuleNames.Where(m => !identities.ContainsKey(m));
            IEnumerable<string> removeIdentities = removedModuleNames.Where(m => identities.ContainsKey(m) &&
                Constants.ModuleIdentityEdgeManagedByValue.Equals(identities[m].ManagedBy, StringComparison.OrdinalIgnoreCase));

            // First remove identities (so that we don't go over the IoTHub limit)
            await Task.WhenAll(removeIdentities.Select(i => this.identityManager.CreateIdentityAsync(i)));

            Identity[] createdIdentities = await Task.WhenAll(createIdentities.Select(i => this.identityManager.CreateIdentityAsync(i)));

            IEnumerable<IModuleIdentity> moduleIdentities = createdIdentities.Concat(identities.Values)
                .Select(m => this.GetModuleIdentity(m));
            return moduleIdentities.ToImmutableDictionary(m => ModuleIdentityHelper.GetModuleName(m.ModuleId));
        }

        IModuleIdentity GetModuleIdentity(Identity identity) =>
            this.identityProviderServiceBuilder.Create(identity.ModuleId, identity.GenerationId, this.workloadUri.ToString());
    }
}
