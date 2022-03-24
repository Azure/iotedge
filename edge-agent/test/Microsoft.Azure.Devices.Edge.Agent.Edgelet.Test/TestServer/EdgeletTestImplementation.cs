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
        readonly ConcurrentDictionary<string, Identity> identities = new ConcurrentDictionary<string, Identity>();
        readonly ConcurrentDictionary<string, ModuleDetails> modules = new ConcurrentDictionary<string, ModuleDetails>();

        public Task<Identity> CreateIdentityAsync(string apiVersion, IdentitySpec identity) =>
            Task.FromResult(this.identities.GetOrAdd(identity.ModuleId, n => new Identity { ModuleId = n, ManagedBy = identity.ManagedBy, GenerationId = Guid.NewGuid().ToString() }));

        public Task<Identity> UpdateIdentityAsync(string apiVersion, string name, UpdateIdentity updateinfo)
        {
            if (this.identities.ContainsKey(name) == false)
            {
                throw new InvalidOperationException("Module not found");
            }

            if (string.IsNullOrEmpty(updateinfo.GenerationId))
            {
                throw new InvalidOperationException("Generation ID not specified");
            }

            var newIdentity = new Identity
            {
                ModuleId = name,
                ManagedBy = updateinfo.ManagedBy,
                GenerationId = updateinfo.GenerationId
            };

            return Task.FromResult(
                this.identities.AddOrUpdate(
                    name,
                    newIdentity,
                    (n, v) => newIdentity));
        }

        public Task<ModuleDetails> CreateModuleAsync(string apiVersion, ModuleSpec module)
        {
            ModuleDetails createdModule = this.modules.GetOrAdd(module.Name, (n) => GetModuleDetails(module));
            return Task.FromResult(createdModule);
        }

        public Task DeleteIdentityAsync(string apiVersion, string name)
        {
            if (!this.identities.TryRemove(name, out Identity _))
            {
                throw new InvalidOperationException("Identity not found");
            }

            return Task.CompletedTask;
        }

        public Task DeleteModuleAsync(string apiVersion, string name)
        {
            if (!this.modules.TryRemove(name, out ModuleDetails _))
            {
                throw new InvalidOperationException("Module not found");
            }

            return Task.CompletedTask;
        }

        public Task PrepareUpdateModuleAsync(string api_version, string name, ModuleSpec module) => Task.CompletedTask;

        public Task<ModuleDetails> GetModuleAsync(string apiVersion, string name)
        {
            if (!this.modules.TryGetValue(name, out ModuleDetails module))
            {
                throw new InvalidOperationException("Module not found");
            }

            return Task.FromResult(module);
        }

        public Task<IdentityList> ListIdentitiesAsync(string apiVersion) =>
            Task.FromResult(new IdentityList { Identities = this.identities.Values.ToList() });

        public Task<ModuleList> ListModulesAsync(string apiVersion) =>
            Task.FromResult(new ModuleList { Modules = this.modules.Values.ToList() });

        public Task RestartModuleAsync(string apiVersion, string name)
        {
            if (!this.modules.TryGetValue(name, out ModuleDetails module))
            {
                throw new InvalidOperationException("Module not found");
            }

            module.Status.RuntimeStatus.Status = "Running";
            return Task.CompletedTask;
        }

        public Task StartModuleAsync(string apiVersion, string name)
        {
            if (!this.modules.TryGetValue(name, out ModuleDetails module))
            {
                throw new InvalidOperationException("Module not found");
            }

            module.Status.RuntimeStatus.Status = "Running";
            return Task.CompletedTask;
        }

        public Task StopModuleAsync(string apiVersion, string name)
        {
            if (!this.modules.TryGetValue(name, out ModuleDetails module))
            {
                throw new InvalidOperationException("Module not found");
            }

            module.Status.RuntimeStatus.Status = "Stopped";
            return Task.CompletedTask;
        }

        public Task<ModuleDetails> UpdateModuleAsync(string apiVersion, string name, bool start, ModuleSpec module)
        {
            if (!this.modules.ContainsKey(name))
            {
                throw new InvalidOperationException("Module not found");
            }

            var moduleDetails = GetModuleDetails(module);
            if (start)
            {
                moduleDetails.Status.RuntimeStatus.Status = "Running";
            }

            this.modules[module.Name] = moduleDetails;
            return Task.FromResult(this.modules[module.Name]);
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

        public Task ModuleLogsAsync(string api_version, string name, bool follow, string tail, int? since) => Task.CompletedTask;

        public Task<SystemInfo> GetSystemInfoAsync(string api_version) => Task.FromResult(new SystemInfo());

        public Task ReprovisionDeviceAsync(string api_version) => Task.CompletedTask;
    }
}
