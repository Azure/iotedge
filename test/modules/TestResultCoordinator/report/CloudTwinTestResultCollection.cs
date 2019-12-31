// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
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
        TestOperationResult current;
        bool isLoaded;

        public CloudTwinTestResultCollection(string source, string serviceClientConnectionString, string moduleId, string trackingId)
        {
            this.registryManager = RegistryManager.CreateFromConnectionString(serviceClientConnectionString);
            this.isLoaded = false;
            this.moduleId = moduleId;
            this.Source = source;
            this.trackingId = trackingId;
        }

        public string Source { get; }

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

                var twinTestResult = new TwinTestResult() { TrackingId = this.trackingId, Properties = twin.Properties.Reported };
                return new TestOperationResult(
                    this.Source,
                    TestOperationResultType.Twin.ToString(),
                    twinTestResult.ToString(),
                    twin.LastActivityTime.HasValue ? twin.LastActivityTime.Value : DateTime.UtcNow);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to get twin for {this.moduleId}");
                return null;
            }
        }
    }
}
