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

        public async Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);
            if (diff.IsEmpty)
            {
                return ImmutableDictionary<string, IModuleIdentity>.Empty;
            }

            // System modules have different module names and identity names. We need to convert module names to module identity names
            // and vice versa, to make sure the right values are being used.
            // TODO - This will fail if the user adds modules with the same module name as a system module - for example a module called
            // edgeHub. We might have to catch such cases and flag them as error (or handle them in some other way).

            IEnumerable<string> updatedModuleIdentites = diff.Updated.Select(m => this.GetModuleIdentityName(m.Name));
            IEnumerable<string> removedModuleIdentites = diff.Removed.Select(m => this.GetModuleIdentityName(m));

            IEnumerable<Module> modules = await this.serviceClient.GetModules();

            // TODO - Temporary because serviceClient.GetModules does not return system modules at the moment
            IEnumerable<Module> modulesAsList = modules as IList<Module> ?? modules.ToList();
            if (!modulesAsList.Any(m => m.Id.Equals(Constants.EdgeHubModuleIdentityName)))
            {
                Module edgeHubModule = await this.serviceClient.GetModule(Constants.EdgeHubModuleIdentityName);
                if (edgeHubModule != null)
                {
                    modulesAsList = modulesAsList.Concat(new[] { edgeHubModule });
                }
            }

            ImmutableDictionary<string, Module> modulesDict = modulesAsList.ToImmutableDictionary(p => p.Id);

            IEnumerable<string> createIdentities = updatedModuleIdentites.Where(m => !modulesDict.ContainsKey(m));
            IEnumerable<string> removeIdentities = removedModuleIdentites.Where(m => modulesDict.ContainsKey(m)
                && string.Equals(modulesDict.GetValueOrDefault(m).ManagedBy, ManagedByEdgeHubValue, StringComparison.OrdinalIgnoreCase));

            // Update any identities that don't have Sas auth type or where the keys are null (this will happen for single device deployments,
            // where the identities of modules are created, but the auth keys are not set)
            IEnumerable<Module> updateIdentities = modulesAsList.Where(
                m => m.Authentication == null
                    || m.Authentication.Type != AuthenticationType.Sas
                    || m.Authentication.SymmetricKey == null
                    || (m.Authentication.SymmetricKey.PrimaryKey == null && m.Authentication.SymmetricKey.SecondaryKey == null))
                    .Select(
                    m =>
                    {
                        m.Authentication = new AuthenticationMechanism
                        {
                            Type = AuthenticationType.Sas
                        };
                        return m;
                    }).ToList();

            IEnumerable<Module> updatedModulesIndentity = await this.UpdateServiceModulesIdentityAsync(removeIdentities, createIdentities, updateIdentities);
            IEnumerable<Module> modulesIndentityAsList = updatedModulesIndentity as IList<Module> ?? updatedModulesIndentity.ToList();
            ImmutableDictionary<string, Module> updatedDict = modulesIndentityAsList.ToImmutableDictionary(p => p.Id);

            IEnumerable<IModuleIdentity> moduleIdentities = modulesIndentityAsList.Concat(modulesAsList.Where(p => !updatedDict.ContainsKey(p.Id))).Select(p => new ModuleIdentity(p.Id, this.GetModuleConnectionString(p)));
            return moduleIdentities.ToImmutableDictionary(m => this.GetModuleName(m.Name));
        }

        private string GetModuleName(string name)
        {
            if (name.Equals(Constants.EdgeHubModuleIdentityName))
            {
                return Constants.EdgeHubModuleName;
            }
            else if (name.Equals(Constants.EdgeAgentModuleIdentityName))
            {
                return Constants.EdgeAgentModuleName;
            }
            return name;
        }

        private string GetModuleIdentityName(string moduleName)
        {
            if (moduleName.Equals(Constants.EdgeHubModuleName))
            {
                return Constants.EdgeHubModuleIdentityName;
            }
            else if (moduleName.Equals(Constants.EdgeAgentModuleName))
            {
                return Constants.EdgeAgentModuleIdentityName;
            }
            return moduleName;
        }

        string GetModuleConnectionString(Module module)
        {
            if (module.Authentication.Type != AuthenticationType.Sas)
            {
                throw new ArgumentException($"Authentication type {module.Authentication.Type} is not supported.");
            }

            EdgeHubConnectionString.EdgeHubConnectionStringBuilder connectionStringBuilder = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(this.deviceConnectionDetails.HostName, this.deviceConnectionDetails.DeviceId)
                .SetModuleId(module.Id)
                .SetSharedAccessKey(module.Authentication.SymmetricKey.PrimaryKey);

            if (!module.Id.Equals(Constants.EdgeHubModuleIdentityName, StringComparison.OrdinalIgnoreCase))
            {
                connectionStringBuilder = connectionStringBuilder
                    .SetGatewayHostName(this.deviceConnectionDetails.GatewayHostName);
            }

            return connectionStringBuilder.Build()
                .ToConnectionString();
        }

        async Task<IEnumerable<Module>> UpdateServiceModulesIdentityAsync(IEnumerable<string> removeIdentities, IEnumerable<string> createIdentities, IEnumerable<Module> updateIdentities)
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
