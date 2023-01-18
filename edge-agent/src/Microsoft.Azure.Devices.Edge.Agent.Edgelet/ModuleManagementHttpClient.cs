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
        const int MaxConcurrentRequests = 5;

        readonly ModuleManagementHttpClientVersioned inner;

        readonly TimeSpan clientPermitTimeout = TimeSpan.FromSeconds(240);
        readonly SemaphoreSlim clientPermit = new SemaphoreSlim(MaxConcurrentRequests);

        public ModuleManagementHttpClient(Uri managementUri, string serverSupportedApiVersion, string clientSupportedApiVersion, Option<TimeSpan> edgeletTimeout)
        {
            Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            Preconditions.CheckNonWhiteSpace(serverSupportedApiVersion, nameof(serverSupportedApiVersion));
            Preconditions.CheckNonWhiteSpace(clientSupportedApiVersion, nameof(clientSupportedApiVersion));
            this.inner = GetVersionedModuleManagement(managementUri, serverSupportedApiVersion, clientSupportedApiVersion, edgeletTimeout);
        }

        public Task<Identity> CreateIdentityAsync(string name, string managedBy) => this.Throttle(() => this.inner.CreateIdentityAsync(name, managedBy));

        public Task<Identity> UpdateIdentityAsync(string name, string generationId, string managedBy) => this.Throttle(() => this.inner.UpdateIdentityAsync(name, generationId, managedBy));

        public Task DeleteIdentityAsync(string name) => this.Throttle(() => this.inner.DeleteIdentityAsync(name));

        public Task<IEnumerable<Identity>> GetIdentities() => this.Throttle(() => this.inner.GetIdentities());

        public Task CreateModuleAsync(ModuleSpec moduleSpec) => this.Throttle(() => this.inner.CreateModuleAsync(moduleSpec));

        public Task StartModuleAsync(string name) => this.Throttle(() => this.inner.StartModuleAsync(name));

        public Task StopModuleAsync(string name) => this.Throttle(() => this.inner.StopModuleAsync(name));

        public Task DeleteModuleAsync(string name) => this.Throttle(() => this.inner.DeleteModuleAsync(name));

        public Task RestartModuleAsync(string name) => this.Throttle(() => this.inner.RestartModuleAsync(name));

        public Task UpdateModuleAsync(ModuleSpec moduleSpec) => this.Throttle(() => this.inner.UpdateModuleAsync(moduleSpec));

        public Task UpdateAndStartModuleAsync(ModuleSpec moduleSpec) => this.Throttle(() => this.inner.UpdateAndStartModuleAsync(moduleSpec));

        public Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken) => this.Throttle(() => this.inner.GetSystemInfoAsync(cancellationToken));

        public Task<SystemResources> GetSystemResourcesAsync() => this.Throttle(() => this.inner.GetSystemResourcesAsync());

        public Task<IEnumerable<ModuleRuntimeInfo>> GetModules<T>(CancellationToken token) => this.Throttle(() => this.inner.GetModules<T>(token));

        public Task PrepareUpdateAsync(ModuleSpec moduleSpec) => this.Throttle(() => this.inner.PrepareUpdateAsync(moduleSpec));

        public Task ReprovisionDeviceAsync() => this.Throttle(() => this.inner.ReprovisionDeviceAsync());

        public Task<Stream> GetModuleLogs(string name, bool follow, Option<int> tail, Option<string> since, Option<string> until, Option<bool> includeTimestamp, CancellationToken cancellationToken) =>
            this.Throttle(() => this.inner.GetModuleLogs(name, follow, tail, since, until, includeTimestamp, cancellationToken));

        public Task<Stream> GetSupportBundle(Option<string> since, Option<string> until, Option<string> iothubHostname, Option<bool> edgeRuntimeOnly, CancellationToken token) =>
            this.Throttle(() => this.inner.GetSupportBundle(since, until, iothubHostname, edgeRuntimeOnly, token));

        public async Task<string> GetProductInfoAsync(CancellationToken token, string baseProductInfo)
        {
            SystemInfo systemInfo = await this.GetSystemInfoAsync(token);

            return $"{baseProductInfo} ({systemInfo.ToQueryString()})";
        }

        internal static ModuleManagementHttpClientVersioned GetVersionedModuleManagement(Uri managementUri, string serverSupportedApiVersion, string clientSupportedApiVersion, Option<TimeSpan> edgeletTimeout)
        {
            ApiVersion supportedVersion = GetSupportedVersion(serverSupportedApiVersion, clientSupportedApiVersion);

            if (supportedVersion == ApiVersion.Version20180628)
            {
                return new Version_2018_06_28.ModuleManagementHttpClient(managementUri, edgeletTimeout);
            }

            if (supportedVersion == ApiVersion.Version20190130)
            {
                return new Version_2019_01_30.ModuleManagementHttpClient(managementUri, edgeletTimeout);
            }

            if (supportedVersion == ApiVersion.Version20191022)
            {
                return new Version_2019_10_22.ModuleManagementHttpClient(managementUri, edgeletTimeout);
            }

            if (supportedVersion == ApiVersion.Version20191105)
            {
                return new Version_2019_11_05.ModuleManagementHttpClient(managementUri, edgeletTimeout);
            }

            if (supportedVersion == ApiVersion.Version20200707)
            {
                return new Version_2020_07_07.ModuleManagementHttpClient(managementUri, edgeletTimeout);
            }

            if (supportedVersion == ApiVersion.Version20211207)
            {
                return new Version_2021_12_07.ModuleManagementHttpClient(managementUri, edgeletTimeout);
            }

            if (supportedVersion == ApiVersion.Version20220803)
            {
                return new Version_2022_08_03.ModuleManagementHttpClient(managementUri, edgeletTimeout);
            }

            return new Version_2018_06_28.ModuleManagementHttpClient(managementUri, edgeletTimeout);
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

        Task Throttle(Func<Task> identityOperation)
        {
            return this.Throttle<bool>(
                            async () =>
                            {
                                await identityOperation();
                                return true;
                            });
        }

        async Task<T> Throttle<T>(Func<Task<T>> identityOperation)
        {
            bool permitAcquired = await this.clientPermit.WaitAsync(this.clientPermitTimeout);
            if (!permitAcquired)
            {
                throw new TimeoutException($"Could not acquire permit to call ModuleManager, hit limit of {MaxConcurrentRequests} concurrent requests");
            }

            try
            {
                return await identityOperation();
            }
            finally
            {
                this.clientPermit.Release();
            }
        }
    }
}
