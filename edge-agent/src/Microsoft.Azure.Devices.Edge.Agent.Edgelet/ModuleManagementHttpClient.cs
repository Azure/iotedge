// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Versioning;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;

    public class ModuleManagementHttpClient : IModuleManager, IIdentityManager
    {
        readonly ModuleManagementHttpClientVersioned inner;

        public ModuleManagementHttpClient(Uri managementUri, string edgeletApiVersion, string edgeletClientApiVersion)
        {
            Preconditions.CheckNonWhiteSpace(edgeletApiVersion, nameof(edgeletApiVersion));
            this.inner = this.GetVersionedModuleManagement(managementUri, edgeletApiVersion, edgeletClientApiVersion);
        }

        public Task<Identity> CreateIdentityAsync(string name, string managedBy) => this.inner.CreateIdentityAsync(name, managedBy);

        public Task<Identity> UpdateIdentityAsync(string name, string generationId, string managedBy) => this.inner.UpdateIdentityAsync(name, generationId, managedBy);

        public Task DeleteIdentityAsync(string name) => this.inner.DeleteIdentityAsync(name);

        public Task<IEnumerable<Identity>> GetIdentities() => this.inner.GetIdentities();

        public Task CreateModuleAsync(ModuleSpec moduleSpec) => this.inner.CreateModuleAsync(moduleSpec);

        public Task StartModuleAsync(string name) => this.inner.StartModuleAsync(name);

        public Task StopModuleAsync(string name) => this.inner.StopModuleAsync(name);

        public Task DeleteModuleAsync(string name) => this.inner.DeleteModuleAsync(name);

        public Task RestartModuleAsync(string name) => this.inner.RestartModuleAsync(name);

        public Task UpdateModuleAsync(ModuleSpec moduleSpec) => this.inner.UpdateModuleAsync(moduleSpec);

        public Task UpdateAndStartModuleAsync(ModuleSpec moduleSpec) => this.inner.UpdateAndStartModuleAsync(moduleSpec);

        public Task<SystemInfo> GetSystemInfoAsync() => this.inner.GetSystemInfoAsync();

        public Task<IEnumerable<ModuleRuntimeInfo>> GetModules<T>(CancellationToken token) => this.inner.GetModules<T>(token);

        public Task PrepareUpdateAsync(ModuleSpec moduleSpec) => this.inner.PrepareUpdateAsync(moduleSpec);

        internal ModuleManagementHttpClientVersioned GetVersionedModuleManagement(Uri managementUri, string edgeletApiVersion, string edgeletClientApiVersion)
        {
            ApiVersion supportedVersion = this.GetSupportedVersion(edgeletApiVersion, edgeletClientApiVersion);
            if (supportedVersion == ApiVersion.Version20180628)
            {
                return new Version_2018_06_28.ModuleManagementHttpClient(managementUri);
            }

            if (supportedVersion == ApiVersion.Version20181230)
            {
                return new Version_2018_12_30.ModuleManagementHttpClient(managementUri);
            }

            return new Version_2018_06_28.ModuleManagementHttpClient(managementUri);
        }

        ApiVersion GetSupportedVersion(string edgeletApiVersion, string edgeletManagementApiVersion)
        {
            var serverVersion = (ApiVersion)edgeletApiVersion;
            var clientVersion = (ApiVersion)edgeletManagementApiVersion;

            return serverVersion.Value < clientVersion.Value ? serverVersion : clientVersion;
        }
    }
}
