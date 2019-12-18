// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class CloudTwinTestResultCollection : ITestResultCollection<TestResultCoordinator.TestOperationResult>
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(CloudTwinTestResultCollection));
        readonly RegistryManager registryManager;
        readonly string moduleId;
        readonly string trackingId;

        Queue<TestResultCoordinator.TestOperationResult> resultQueue;
        TestResultCoordinator.TestOperationResult current;
        bool loadedFromCloud;

        public CloudTwinTestResultCollection(string source, string serviceClientConnectionString, string moduleId, string trackingId)
        {
            this.registryManager = RegistryManager.CreateFromConnectionString(serviceClientConnectionString);
            this.resultQueue = new Queue<TestResultCoordinator.TestOperationResult>();
            this.loadedFromCloud = false;
            this.moduleId = moduleId;
            this.Source = source;
            this.trackingId = trackingId;
        }

        public string Source { get; }

        TestResultCoordinator.TestOperationResult ITestResultCollection<TestResultCoordinator.TestOperationResult>.Current => this.current;

        public void Dispose()
        {
            this.registryManager.Dispose();
        }

        public async Task<bool> MoveNextAsync()
        {
            bool hasValue = this.GetFromQueue();
            if (!hasValue && !this.loadedFromCloud)
            {
                await this.GetTwinAsync();
                this.loadedFromCloud = true;
                hasValue = this.GetFromQueue();
            }

            return hasValue;
        }

        public void Reset()
        {
            this.loadedFromCloud = false;
            this.resultQueue = new Queue<TestResultCoordinator.TestOperationResult>();
        }

        bool GetFromQueue()
        {
            if (this.resultQueue.Count > 0)
            {
                this.current = this.resultQueue.Dequeue();
                return true;
            }
            else
            {
                this.current = default(TestResultCoordinator.TestOperationResult);
                return false;
            }
        }

        async Task GetTwinAsync()
        {
            try
            {
                Twin twin = await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, this.moduleId);
                if (twin == null)
                {
                    Logger.LogError($"Twin was null for {this.moduleId}");
                    return;
                }

                var twinTestResult = new TwinTestResult() { TrackingId = this.trackingId, Properties = twin.Properties.Reported };
                this.resultQueue.Enqueue(
                    new TestResultCoordinator.TestOperationResult(
                        this.Source,
                        TestOperationResultType.Twin.ToString(),
                        twinTestResult.ToString(),
                        twin.LastActivityTime.HasValue ? twin.LastActivityTime.Value : DateTime.UtcNow));
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to get twin for {this.moduleId}");
            }
        }
    }
}
