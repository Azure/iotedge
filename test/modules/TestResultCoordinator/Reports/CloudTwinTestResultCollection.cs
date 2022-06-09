// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class CloudTwinTestResultCollection : IAsyncEnumerable<TestOperationResult>
    {
        CloudTwinTestResultCollectionEnumerator enumerator;

        public CloudTwinTestResultCollection(string source, string serviceClientConnectionString, string moduleId, string trackingId)
        {
            CloudTwinTestResultCollectionEnumerator enumerator = new CloudTwinTestResultCollectionEnumerator(source, serviceClientConnectionString, moduleId, trackingId);
            this.enumerator = enumerator;
        }

        public IAsyncEnumerator<TestOperationResult> GetAsyncEnumerator(CancellationToken _)
        {
            return this.enumerator;
        }

        public class CloudTwinTestResultCollectionEnumerator : IAsyncEnumerator<TestOperationResult>
        {
            static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(CloudTwinTestResultCollection));
            readonly RegistryManager registryManager;
            readonly string moduleId;
            readonly string trackingId;
            readonly string source;
            TestOperationResult current;
            bool isLoaded;

            internal CloudTwinTestResultCollectionEnumerator(string source, string serviceClientConnectionString, string moduleId, string trackingId)
            {
                this.registryManager = RegistryManager.CreateFromConnectionString(serviceClientConnectionString);
                this.isLoaded = false;
                this.moduleId = moduleId;
                this.source = source;
                this.trackingId = trackingId;
                this.current = default(TestOperationResult);
            }

            public TestOperationResult Current => this.current;

            public ValueTask DisposeAsync()
            {
                RegistryManager rm = this.registryManager;
                return new ValueTask(Task.Run(() => rm.Dispose()));
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                if (!this.isLoaded)
                {
                    this.current = await this.GetTwinAsync();
                    this.isLoaded = true;
                }
                else
                {
                    this.current = null;
                }

                return this.current != null;
            }

            public void Reset()
            {
                this.isLoaded = false;
                this.current = null;
            }

            async Task<TestOperationResult> GetTwinAsync()
            {
                try
                {
                    Twin twin = await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, this.moduleId);
                    if (twin == null)
                    {
                        Logger.LogError($"Twin was null for {this.moduleId}");
                        return null;
                    }

                    Logger.LogDebug($"Twin reported properties from cloud {twin.Properties.Reported}");

                    return new TwinTestResult(this.source, twin.LastActivityTime.HasValue ? twin.LastActivityTime.Value : DateTime.UtcNow)
                    {
                        TrackingId = this.trackingId,
                        Properties = twin.Properties.Reported
                    }.ToTestOperationResult();
                }
                catch (Exception e)
                {
                    Logger.LogError(e, $"Failed to get twin for {this.moduleId}");
                    return null;
                }
            }
        }
    }
}
