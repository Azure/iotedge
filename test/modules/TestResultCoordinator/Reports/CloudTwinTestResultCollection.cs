// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class CloudTwinTestResultCollection : ITestResultCollection<TestOperationResult>
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(CloudTwinTestResultCollection));
        readonly RegistryManager registryManager;
        readonly string moduleId;
        readonly string trackingId;
        readonly string source;
        TestOperationResult current;
        bool isLoaded;

        public CloudTwinTestResultCollection(string source, string serviceClientConnectionString, string moduleId, string trackingId)
        {
            this.registryManager = RegistryManager.CreateFromConnectionString(serviceClientConnectionString);
            this.isLoaded = false;
            this.moduleId = moduleId;
            this.source = source;
            this.trackingId = trackingId;
        }

        TestOperationResult ITestResultCollection<TestOperationResult>.Current => this.current;

        public void Dispose()
        {
            this.registryManager.Dispose();
        }

        public async Task<bool> MoveNextAsync()
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
