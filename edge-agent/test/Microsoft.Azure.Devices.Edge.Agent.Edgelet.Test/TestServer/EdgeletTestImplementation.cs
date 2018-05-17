// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test.TestServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test.TestServer.Controllers;

    public class EdgeletTestImplementation : IController
    {
        ConcurrentDictionary<string, Identity> identities = new ConcurrentDictionary<string, Identity>();
        ConcurrentDictionary<string, ModuleDetails> modules = new ConcurrentDictionary<string, ModuleDetails>();

        public Task<Identity> CreateIdentityAsync(string api_version, string name, IdentitySpec identity) =>
            Task.FromResult(this.identities.GetOrAdd(name, (n) => new Identity { ModuleId = n, ManagedBy = "IotEdge", GenerationId = Guid.NewGuid().ToString() }));

        public Task<ModuleDetails> CreateModuleAsync(string api_version, ModuleSpec module)
        {
            ModuleDetails createdModule = this.modules.GetOrAdd(module.Name, (n) => GetModuleDetails(module));
            return Task.FromResult(createdModule);
        }

        static ModuleDetails GetModuleDetails(ModuleSpec moduleSpec)
        {
            var moduleDetails = new ModuleDetails
            {
                Id = Guid.NewGuid().ToString(),
                Name = moduleSpec.Name,
                Type = moduleSpec.Type,
                Status = new Status { ExitStatus = null, RuntimeStatus = new RuntimeStatus { Status = "Created", Description = "Created" }, StartTime = null },
                Config = moduleSpec.Config
            };
            return moduleDetails;
        }

        public Task DeleteIdentityAsync(string api_version, string name)
        {
            if (!this.identities.TryRemove(name, out Identity _))
            {
                throw new InvalidOperationException("Identity not found");
            }
            return Task.CompletedTask;
        }

        public Task DeleteModuleAsync(string api_version, string name)
        {
            if (!this.modules.TryRemove(name, out ModuleDetails _))
            {
                throw new InvalidOperationException("Module not found");
            }
            return Task.CompletedTask;
        }

        public Task<ModuleDetails> GetModuleAsync(string api_version, string name)
        {
            if (!this.modules.TryGetValue(name, out ModuleDetails module))
            {
                throw new InvalidOperationException("Module not found");
            }
            return Task.FromResult(module);
        }

        public Task<IdentityList> ListIdentitiesAsync(string api_version) =>
            Task.FromResult(new IdentityList { Identities = this.identities.Values.ToList() });

        public Task<ModuleList> ListModulesAsync(string api_version) =>
            Task.FromResult(new ModuleList { Modules = this.modules.Values.ToList() });

        public Task RestartModuleAsync(string api_version, string name)
        {
            if (!this.modules.TryGetValue(name, out ModuleDetails module))
            {
                throw new InvalidOperationException("Module not found");
            }
            module.Status.RuntimeStatus.Status = "Running";
            return Task.CompletedTask;
        }

        public Task StartModuleAsync(string api_version, string name)
        {
            if (!this.modules.TryGetValue(name, out ModuleDetails module))
            {
                throw new InvalidOperationException("Module not found");
            }
            module.Status.RuntimeStatus.Status = "Running";
            return Task.CompletedTask;
        }

        public Task StopModuleAsync(string api_version, string name)
        {
            if (!this.modules.TryGetValue(name, out ModuleDetails module))
            {
                throw new InvalidOperationException("Module not found");
            }
            module.Status.RuntimeStatus.Status = "Stopped";
            return Task.CompletedTask;
        }

        public Task<ModuleDetails> UpdateModuleAsync(string api_version, string name, ModuleSpec module)
        {
            if (!this.modules.ContainsKey(name))
            {
                throw new InvalidOperationException("Module not found");
            }
            this.modules[module.Name] = GetModuleDetails(module);
            return Task.FromResult(this.modules[module.Name]);
        }
    }
}
