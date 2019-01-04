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
    using Microsoft.Extensions.Logging;

    public class ModuleIdentityLifecycleManager : IModuleIdentityLifecycleManager
    {
        readonly IServiceClient serviceClient;
        readonly string iothubHostName;
        readonly string deviceId;
        readonly string gatewayHostName;

        public ModuleIdentityLifecycleManager(
            IServiceClient serviceClient,
            string iothubHostName,
            string deviceId,
            string gatewayHostName)
        {
            this.serviceClient = Preconditions.CheckNotNull(serviceClient, nameof(serviceClient));
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
        }

        // Modules in IoTHub can be created in one of two ways - 1. single deployment:
        // This can be done using the registry manager's
        // ApplyConfigurationContentOnDeviceAsync method. This call will create all
        // modules in the provided deployment json, but does not create the module
        // credentials. After a single deployment, GetModuleIdentitiesAsync will update
        // such modules in the service by calling UpdateModuleAsync, prompting the
        // service to create and return credentials for them. The single deployment also
        // stamps each module with its twin (provided by the deployment json) at module
        // creation time. 2. at-scale deployment: This can be done via the portal on the
        // Edge blade. This type of deployment waits for a module identity to be
        // created, before stamping it with its twin. In this type of deployment, the
        // EdgeAgent needs to create the modules identities. This is also handled in
        // GetModuleIdentitiesAsync. When the deployment detects that a module has been
        // created, it stamps it with the deployed twin. The service creates the
        // $edgeAgent and $edgeHub twin when it creates the Edge Device, so their twins
        // are always available for stamping with either a single deployment or at-scale
        // deployment.
        public async Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(ModuleSet desired, ModuleSet current)
        {
            Diff diff = desired.Diff(current);
            if (diff.IsEmpty)
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
            // System modules have different module names and identity names. We need to convert module names to module identity names
            // and vice versa, to make sure the right values are being used.
            // TODO - This will fail if the user adds modules with the same module name as a system module - for example a module called
            // edgeHub. We might have to catch such cases and flag them as error (or handle them in some other way).
            IEnumerable<string> updatedModuleIdentites = diff.Updated.Select(m => ModuleIdentityHelper.GetModuleIdentityName(m.Name));
            IEnumerable<string> removedModuleIdentites = diff.Removed.Select(m => ModuleIdentityHelper.GetModuleIdentityName(m));

            List<Module> modules = (await this.serviceClient.GetModules()).ToList();

            ImmutableDictionary<string, Module> modulesDict = modules.ToImmutableDictionary(p => p.Id);

            IEnumerable<string> createIdentities = updatedModuleIdentites.Where(m => !modulesDict.ContainsKey(m));
            IEnumerable<string> removeIdentities = removedModuleIdentites.Where(
                m => modulesDict.ContainsKey(m)
                     && string.Equals(modulesDict.GetValueOrDefault(m).ManagedBy, Constants.ModuleIdentityEdgeManagedByValue, StringComparison.OrdinalIgnoreCase));

            // Update any identities that don't have SAS auth type or where the keys are null (this will happen for single device deployments,
            // where the identities of modules are created, but the auth keys are not set).
            IEnumerable<Module> updateIdentities = modules.Where(
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

            List<Module> updatedModulesIndentity = (await this.UpdateServiceModulesIdentityAsync(removeIdentities, createIdentities, updateIdentities)).ToList();
            ImmutableDictionary<string, Module> updatedDict = updatedModulesIndentity.ToImmutableDictionary(p => p.Id);

            IEnumerable<IModuleIdentity> moduleIdentities = updatedModulesIndentity.Concat(modules.Where(p => !updatedDict.ContainsKey(p.Id))).Select(
                p =>
                {
                    string connectionString = this.GetModuleConnectionString(p);
                    return new ModuleIdentity(this.iothubHostName, this.gatewayHostName, this.deviceId, p.Id, new ConnectionStringCredentials(connectionString));
                });
            return moduleIdentities.ToImmutableDictionary(m => ModuleIdentityHelper.GetModuleName(m.ModuleId));
        }

        string GetModuleConnectionString(Module module)
        {
            if (module.Authentication.Type != AuthenticationType.Sas)
            {
                throw new ArgumentException($"Authentication type {module.Authentication.Type} is not supported.");
            }

            ModuleConnectionStringBuilder.ModuleConnectionString moduleConnectionString = new ModuleConnectionStringBuilder(this.iothubHostName, this.deviceId)
                .Create(module.Id)
                .WithSharedAccessKey(module.Authentication.SymmetricKey.PrimaryKey);

            return module.Id.Equals(Constants.EdgeHubModuleIdentityName, StringComparison.OrdinalIgnoreCase)
                ? moduleConnectionString
                : moduleConnectionString.WithGatewayHostName(this.gatewayHostName);
        }

        async Task<Module[]> UpdateServiceModulesIdentityAsync(IEnumerable<string> removeIdentities, IEnumerable<string> createIdentities, IEnumerable<Module> updateIdentities)
        {
            await this.serviceClient.RemoveModules(removeIdentities);

            Module[] identities = (await Task.WhenAll(
                    this.serviceClient.CreateModules(createIdentities),
                    this.serviceClient.UpdateModules(updateIdentities)))
                .Aggregate((l1, l2) => l1.Concat(l2).ToArray());

            return identities;
        }

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
