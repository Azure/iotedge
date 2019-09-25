// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class ModuleIdentityLifecycleManager : IModuleIdentityLifecycleManager
    {
        readonly IIdentityManager identityManager;
        readonly ModuleIdentityProviderServiceBuilder identityProviderServiceBuilder;
        readonly Uri workloadUri;

        protected virtual bool ShouldAlwaysReturnIdentities => false;

        public ModuleIdentityLifecycleManager(IIdentityManager identityManager, ModuleIdentityProviderServiceBuilder identityProviderServiceBuilder, Uri workloadUri)
        {
            this.identityManager = Preconditions.CheckNotNull(identityManager, nameof(identityManager));
            this.identityProviderServiceBuilder = Preconditions.CheckNotNull(identityProviderServiceBuilder, nameof(identityProviderServiceBuilder));
            this.workloadUri = Preconditions.CheckNotNull(workloadUri, nameof(workloadUri));
        }

        public async Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);
            if (diff.IsEmpty && !this.ShouldAlwaysReturnIdentities)
            {
                return ImmutableDictionary<string, IModuleIdentity>.Empty;
            }

            try
            {
                IImmutableDictionary<string, IModuleIdentity> moduleIdentities = await this.GetModuleIdentitiesAsync(diff);
                return moduleIdentities;
            }
            catch (Exception ex)
            {
                Events.ErrorGettingModuleIdentities(ex);
                return ImmutableDictionary<string, IModuleIdentity>.Empty;
            }
        }

        async Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(Diff diff)
        {
            IList<string> addedOrUpdatedModuleNames = diff.AddedOrUpdated.Select(m => ModuleIdentityHelper.GetModuleIdentityName(m.Name)).ToList();
            IEnumerable<string> removedModuleNames = diff.Removed.Select(ModuleIdentityHelper.GetModuleIdentityName);

            IImmutableDictionary<string, Identity> identities = (await this.identityManager.GetIdentities()).ToImmutableDictionary(i => i.ModuleId);

            // Create identities for all modules that are in the deployment but aren't in iotedged.
            IEnumerable<string> createIdentities = addedOrUpdatedModuleNames.Where(m => !identities.ContainsKey(m));

            // Update identities for all modules that are in the deployment and are in iotedged (except for Edge Agent which gets special
            // treatment in iotedged).
            //
            // NOTE: This update can potentiatlly be made more efficient by checking that an update is actually needed, i.e. if auth type
            // is not SAS and/or if the credentials are not what iotedged expects it to be.
            IEnumerable<Identity> updateIdentities = addedOrUpdatedModuleNames
                .Where(m => identities.ContainsKey(m) && m != Constants.EdgeAgentModuleIdentityName)
                .Select(m => identities[m]);

            // Remove identities which exist in iotedged but don't exist in the deployment anymore. We exclude however, identities that
            // aren't managed by Edge since these have been created by some out-of-band process and Edge doesn't "own" the identity.
            IEnumerable<string> removeIdentities = removedModuleNames.Where(
                m => identities.ContainsKey(m) &&
                     Constants.ModuleIdentityEdgeManagedByValue.Equals(identities[m].ManagedBy, StringComparison.OrdinalIgnoreCase));

            // First remove identities (so that we don't go over the IoTHub limit).
            await Task.WhenAll(removeIdentities.Select(i => this.identityManager.DeleteIdentityAsync(i)));

            // Create/update identities.
            IEnumerable<Task<Identity>> createTasks = createIdentities.Select(i => this.identityManager.CreateIdentityAsync(i, Constants.ModuleIdentityEdgeManagedByValue));
            IEnumerable<Task<Identity>> updateTasks = updateIdentities.Select(i => this.identityManager.UpdateIdentityAsync(i.ModuleId, i.GenerationId, i.ManagedBy));
            Identity[] upsertedIdentities = await Task.WhenAll(createTasks.Concat(updateTasks));

            List<IModuleIdentity> moduleIdentities;

            moduleIdentities = upsertedIdentities.Select(m => this.GetModuleIdentity(m)).ToList();

            // Add back the unchanged identities (including Edge Agent).
            var upsertedIdentityList = moduleIdentities.Select(i => i.ModuleId).ToList();
            var unchangedIdentities = identities.Where(i => !removedModuleNames.Contains(i.Key) && !upsertedIdentityList.Contains(i.Key));
            moduleIdentities.AddRange(unchangedIdentities.Select(i => this.GetModuleIdentity(i.Value)));

            return moduleIdentities.ToImmutableDictionary(m => ModuleIdentityHelper.GetModuleName(m.ModuleId));
        }

        IModuleIdentity GetModuleIdentity(Identity identity) =>
            this.identityProviderServiceBuilder.Create(identity.ModuleId, identity.GenerationId, this.workloadUri.ToString());

        static class Events
        {
            const int IdStart = AgentEventIds.ModuleIdentityLifecycleManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleIdentityLifecycleManager>();

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
