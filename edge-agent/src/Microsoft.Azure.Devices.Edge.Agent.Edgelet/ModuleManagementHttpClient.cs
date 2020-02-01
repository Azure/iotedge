// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Versioning;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;

    public class ModuleManagementHttpClient : IModuleManager, IIdentityManager, IDeviceManager
    {
        readonly ModuleManagementHttpClientVersioned inner;

        public ModuleManagementHttpClient(Uri managementUri, string serverSupportedApiVersion, string clientSupportedApiVersion)
        {
            Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            Preconditions.CheckNonWhiteSpace(serverSupportedApiVersion, nameof(serverSupportedApiVersion));
            Preconditions.CheckNonWhiteSpace(clientSupportedApiVersion, nameof(clientSupportedApiVersion));
            this.inner = GetVersionedModuleManagement(managementUri, serverSupportedApiVersion, clientSupportedApiVersion);
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

        public Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken) => this.inner.GetSystemInfoAsync(cancellationToken);

        public Task<SystemResources> GetSystemResourcesAsync() => this.inner.GetSystemResourcesAsync();

        public Task<IEnumerable<ModuleRuntimeInfo>> GetModules<T>(CancellationToken token) => this.inner.GetModules<T>(token);

        public Task PrepareUpdateAsync(ModuleSpec moduleSpec) => this.inner.PrepareUpdateAsync(moduleSpec);

        public Task ReprovisionDeviceAsync() => this.inner.ReprovisionDeviceAsync();

        public Task<Stream> GetModuleLogs(string name, bool follow, Option<int> tail, Option<int> since, CancellationToken cancellationToken) =>
            this.inner.GetModuleLogs(name, follow, tail, since, cancellationToken);

        internal static ModuleManagementHttpClientVersioned GetVersionedModuleManagement(Uri managementUri, string serverSupportedApiVersion, string clientSupportedApiVersion)
        {
            ApiVersion supportedVersion = GetSupportedVersion(serverSupportedApiVersion, clientSupportedApiVersion);
            if (supportedVersion == ApiVersion.Version20180628)
            {
                return new Version_2018_06_28.ModuleManagementHttpClient(managementUri);
            }

            if (supportedVersion == ApiVersion.Version20190130)
            {
                return new Version_2019_01_30.ModuleManagementHttpClient(managementUri);
            }

            if (supportedVersion == ApiVersion.Version20191022)
            {
                return new Version_2019_10_22.ModuleManagementHttpClient(managementUri);
            }

            if (supportedVersion == ApiVersion.Version20191105)
            {
                return new Version_2019_11_05.ModuleManagementHttpClient(managementUri);
            }

            return new Version_2018_06_28.ModuleManagementHttpClient(managementUri);
        }

        static ApiVersion GetSupportedVersion(string serverSupportedApiVersion, string clientSupportedApiVersion)
        {
            var serverVersion = ApiVersion.ParseVersion(serverSupportedApiVersion);
            var clientVersion = ApiVersion.ParseVersion(clientSupportedApiVersion);

            if (clientVersion == ApiVersion.VersionUnknown)
            {
                throw new InvalidOperationException("Client version is not supported.");
            }

            if (serverVersion == ApiVersion.VersionUnknown)
            {
                return clientVersion;
            }

            return serverVersion.Value < clientVersion.Value ? serverVersion : clientVersion;
        }
    }
}
