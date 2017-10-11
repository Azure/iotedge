// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleIdentityLifecycleManager : IModuleIdentityLifecycleManager
    {
        static readonly string ManagedByEdgeHubValue = "IotEdge";
        readonly IServiceClient serviceClient;
        readonly EdgeHubConnectionString deviceConnectionDetails;

        public ModuleIdentityLifecycleManager(IServiceClient serviceClient, EdgeHubConnectionString connectionDetails)
        {
            this.serviceClient = Preconditions.CheckNotNull(serviceClient, nameof(serviceClient));
            this.deviceConnectionDetails = Preconditions.CheckNotNull(connectionDetails, nameof(connectionDetails));
        }

        public async Task<IEnumerable<IModuleIdentity>> UpdateModulesIdentity(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);
            if (diff.IsEmpty)
            {
                return new List<IModuleIdentity>();
            }

            IEnumerable<Module> modules = await this.serviceClient.GetModules();
            ImmutableDictionary<string, Module> modulesDict = modules.ToImmutableDictionary(p => p.Id);

            IEnumerable<string> createIdentities = diff.Updated.Where(m => !modulesDict.ContainsKey(m.Name)).Select(m => m.Name);
            IEnumerable<string> removeIdentities = diff.Removed.Where(m => modulesDict.ContainsKey(m)
                && string.Equals(modulesDict.GetValueOrDefault(m).ManagedBy, ManagedByEdgeHubValue, StringComparison.OrdinalIgnoreCase)).Select(m => m);

            IEnumerable<Module> updateIdentities = modules.Where(
                m => m.Authentication == null
                    || m.Authentication.Type != AuthenticationType.Sas
                    || m.Authentication.SymmetricKey == null
                    || (m.Authentication.SymmetricKey.PrimaryKey == null && m.Authentication.SymmetricKey.SecondaryKey == null))
                    .Select(
                    m =>
                    {
                        m.Authentication = new AuthenticationMechanism();
                        m.Authentication.Type = AuthenticationType.Sas;
                        return m;
                    }).ToList();

            IEnumerable<Module> updatedModulesIndentity = await this.UpdateServiceModulesIdentity(removeIdentities, createIdentities, updateIdentities);
            ImmutableDictionary<string, Module> updatedDict = updatedModulesIndentity.ToImmutableDictionary(p => p.Id);

            return updatedModulesIndentity.Concat(modules.Where(p => !updatedDict.ContainsKey(p.Id))).Select(p => new ModuleIdentity(p.Id, this.GetModuleConnectionString(p)));
        }

        string GetModuleConnectionString(Module module)
        {
            if (module.Authentication.Type != AuthenticationType.Sas)
            {
                throw new ArgumentException($"Authentication type {module.Authentication.Type} is not supported.");
            }

            return new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(this.deviceConnectionDetails.HostName, this.deviceConnectionDetails.DeviceId)
                .SetModuleId(module.Id)
                .SetSharedAccessKey(module.Authentication.SymmetricKey.PrimaryKey)
                .Build()
                .ToConnectionString();
        }

        async Task<IEnumerable<Module>> UpdateServiceModulesIdentity(IEnumerable<string> removeIdentities, IEnumerable<string> createIdentities, IEnumerable<Module> updateIdentities)
        {
            await this.serviceClient.RemoveModules(removeIdentities);

            IEnumerable<Module> identities = (await Task.WhenAll(
                this.serviceClient.CreateModules(createIdentities),
                this.serviceClient.UpdateModules(updateIdentities)
            )).Aggregate((l1, l2) => l1.Concat(l2).ToArray());

            return identities;
        }
    }
}
