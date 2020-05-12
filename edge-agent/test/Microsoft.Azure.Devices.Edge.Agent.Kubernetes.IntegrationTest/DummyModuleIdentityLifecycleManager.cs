// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public class DummyModuleIdentityLifecycleManager : IModuleIdentityLifecycleManager
    {
        readonly string hostName;
        readonly string edgeDeviceHostname;
        readonly string deviceId;
        readonly string moduleId;
        readonly ICredentials credentials;
        private IImmutableDictionary<string, IModuleIdentity> identites = ImmutableDictionary<string, IModuleIdentity>.Empty;

        public DummyModuleIdentityLifecycleManager(string hostName, string edgeDeviceHostname, string deviceId, string moduleId, ICredentials credentials)
        {
            this.hostName = hostName;
            this.edgeDeviceHostname = edgeDeviceHostname;
            this.deviceId = deviceId;
            this.moduleId = moduleId;
            this.credentials = credentials;
        }

        public Task<IImmutableDictionary<string, IModuleIdentity>> GetModuleIdentitiesAsync(ModuleSet desired, ModuleSet current) => Task.FromResult(this.identites);

        public void SetModules(params string[] moduleNames) => this.identites = moduleNames
            .Select(name => new { Name = name, ModuleId = this.CreateModuleIdentity() })
            .ToImmutableDictionary(id => id.Name, id => id.ModuleId);

        IModuleIdentity CreateModuleIdentity() => new ModuleIdentity(this.hostName, this.deviceId, this.edgeDeviceHostname, this.GetGatewayHostname(this.moduleId), this.moduleId, this.credentials);

        string GetGatewayHostname(string moduleId)
        {
            if (moduleId.Equals(Constants.EdgeAgentModuleIdentityName, StringComparison.OrdinalIgnoreCase) ||
                moduleId.Equals(Constants.EdgeHubModuleIdentityName, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return this.edgeDeviceHostname;
        }
    }
}
