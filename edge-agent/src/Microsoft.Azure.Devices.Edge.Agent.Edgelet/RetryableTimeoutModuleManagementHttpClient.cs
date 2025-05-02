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
    using Microsoft.Azure.Devices.Edge.Util;

    public class RetryableTimeoutModuleManagementHttpClient : IModuleManager, IIdentityManager, IDeviceManager
    {
        const int MaxRetries = 3;
        const int InitialDelay = 1000;
        const int MaxDelay = 10000;

        readonly ModuleManagementHttpClient inner;

        public RetryableTimeoutModuleManagementHttpClient(Uri managementUri, string serverSupportedApiVersion, string clientSupportedApiVersion, Option<TimeSpan> edgeletTimeout)
        {
            Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            Preconditions.CheckNonWhiteSpace(serverSupportedApiVersion, nameof(serverSupportedApiVersion));
            Preconditions.CheckNonWhiteSpace(clientSupportedApiVersion, nameof(clientSupportedApiVersion));
            this.inner = new ModuleManagementHttpClient(managementUri, serverSupportedApiVersion, clientSupportedApiVersion, edgeletTimeout);
        }

        public Task<Identity> CreateIdentityAsync(string name, string managedBy) => this.Retry(() => this.inner.CreateIdentityAsync(name, managedBy));

        public Task<Identity> UpdateIdentityAsync(string name, string generationId, string managedBy) => this.Retry(() => this.inner.UpdateIdentityAsync(name, generationId, managedBy));

        public Task DeleteIdentityAsync(string name) => this.Retry(() => this.inner.DeleteIdentityAsync(name));

        public Task<IEnumerable<Identity>> GetIdentities() => this.Retry(() => this.inner.GetIdentities());

        public Task CreateModuleAsync(ModuleSpec moduleSpec) => this.Retry(() => this.inner.CreateModuleAsync(moduleSpec));

        public Task StartModuleAsync(string name) => this.Retry(() => this.inner.StartModuleAsync(name));

        public Task StopModuleAsync(string name) => this.Retry(() => this.inner.StopModuleAsync(name));

        public Task DeleteModuleAsync(string name) => this.Retry(() => this.inner.DeleteModuleAsync(name));

        public Task RestartModuleAsync(string name) => this.Retry(() => this.inner.RestartModuleAsync(name));

        public Task UpdateModuleAsync(ModuleSpec moduleSpec) => this.Retry(() => this.inner.UpdateModuleAsync(moduleSpec));

        public Task UpdateAndStartModuleAsync(ModuleSpec moduleSpec) => this.Retry(() => this.inner.UpdateAndStartModuleAsync(moduleSpec));

        public Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken) => this.Retry(() => this.inner.GetSystemInfoAsync(cancellationToken));

        public Task<SystemResources> GetSystemResourcesAsync() => this.Retry(() => this.inner.GetSystemResourcesAsync());

        public Task<IEnumerable<ModuleRuntimeInfo>> GetModules<T>(CancellationToken token) => this.Retry(() => this.inner.GetModules<T>(token));

        public Task PrepareUpdateAsync(ModuleSpec moduleSpec) => this.Retry(() => this.inner.PrepareUpdateAsync(moduleSpec));

        public Task ReprovisionDeviceAsync() => this.Retry(() => this.inner.ReprovisionDeviceAsync());

        public Task<Stream> GetModuleLogs(string name, bool follow, Option<int> tail, Option<string> since, Option<string> until, Option<bool> includeTimestamp, CancellationToken cancellationToken) =>
            this.Retry(() => this.inner.GetModuleLogs(name, follow, tail, since, until, includeTimestamp, cancellationToken));

        public Task<Stream> GetSupportBundle(Option<string> since, Option<string> until, Option<string> iothubHostname, Option<bool> edgeRuntimeOnly, CancellationToken token) =>
            this.Retry(() => this.inner.GetSupportBundle(since, until, iothubHostname, edgeRuntimeOnly, token));

        public Task<string> GetProductInfoAsync(CancellationToken token, string baseProductInfo) =>
            this.inner.GetProductInfoAsync(token, baseProductInfo);

        async Task<T> Retry<T>(Func<Task<T>> op)
        {
            var retryCount = 0;
            while (retryCount < MaxRetries)
            {
                try
                {
                    return await op();
                }
                catch (TimeoutException)
                {
                    retryCount += 1;
                    await Task.Delay(Math.Min(InitialDelay * retryCount, MaxDelay));
                }
                catch (Exception ex)
                {
                    // Rethrow exception
                    throw ex;
                }
            }

            throw new TimeoutException($"Max retries of {MaxRetries} exceeded");
        }
    }
}
