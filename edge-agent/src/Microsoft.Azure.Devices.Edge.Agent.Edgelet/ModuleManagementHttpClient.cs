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
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Versioning;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;

    public class ModuleManagementHttpClient : IModuleManager, IIdentityManager, IDeviceManager
    {
        readonly ModuleManagementHttpClientVersioned inner;

        readonly TimeSpan clientTicketTimeout = TimeSpan.FromSeconds(240);
        readonly TimeSpan operationDelay = TimeSpan.FromSeconds(0.7);
        readonly SemaphoreSlim clientTicket = new SemaphoreSlim(1);

        public ModuleManagementHttpClient(Uri managementUri, string serverSupportedApiVersion, string clientSupportedApiVersion)
        {
            Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            Preconditions.CheckNonWhiteSpace(serverSupportedApiVersion, nameof(serverSupportedApiVersion));
            Preconditions.CheckNonWhiteSpace(clientSupportedApiVersion, nameof(clientSupportedApiVersion));
            this.inner = GetVersionedModuleManagement(managementUri, serverSupportedApiVersion, clientSupportedApiVersion);
        }

        public Task<Identity> CreateIdentityAsync(string name, string managedBy) => this.Throttle(() => this.inner.CreateIdentityAsync(name, managedBy));

        public Task<Identity> UpdateIdentityAsync(string name, string generationId, string managedBy) => this.Throttle(() => this.inner.UpdateIdentityAsync(name, generationId, managedBy));

        public Task DeleteIdentityAsync(string name) => this.Throttle(() => this.inner.DeleteIdentityAsync(name));

        public Task<IEnumerable<Identity>> GetIdentities() => this.Throttle(() => this.inner.GetIdentities());

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

        public Task<Stream> GetModuleLogs(string name, bool follow, Option<int> tail, Option<string> since, Option<string> until, Option<bool> includeTimestamp, CancellationToken cancellationToken) =>
            this.inner.GetModuleLogs(name, follow, tail, since, until, includeTimestamp, cancellationToken);

        public Task<Stream> GetSupportBundle(Option<string> since, Option<string> until, Option<string> iothubHostname, Option<bool> edgeRuntimeOnly, CancellationToken token) =>
            this.inner.GetSupportBundle(since, until, iothubHostname, edgeRuntimeOnly, token);

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

            if (supportedVersion == ApiVersion.Version20200707)
            {
                return new Version_2020_07_07.ModuleManagementHttpClient(managementUri);
            }

            return new Version_2018_06_28.ModuleManagementHttpClient(managementUri);
        }

        Task Throttle(Func<Task> func) => this.Throttle<bool>(
            async () =>
            {
                await func();
                return true;
            });

        async Task<T> Throttle<T>(Func<Task<T>> func)
        {
            await this.clientTicket.WaitAsync(this.clientTicketTimeout);
            try
            {
                var start = DateTime.Now;

                var result = await func();

                var operationDuration = DateTime.Now - start;
                if (operationDuration < this.operationDelay)
                {
                    var remainingDelay = this.operationDelay - operationDuration;
                    await Task.Delay(remainingDelay);
                }

                return result;
            }
            finally
            {
                this.clientTicket.Release();
            }
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
