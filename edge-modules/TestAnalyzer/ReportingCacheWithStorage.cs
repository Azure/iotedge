// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ReportingCacheWithStorage
    {
        TestStatusStorage storage;

        public static ReportingCacheWithStorage Instance { get; } = new ReportingCacheWithStorage();

        ReportingCacheWithStorage()
        {
        }

        public async Task InitAsync(string storagePath, bool optimizeForPerformance)
        {
            this.storage = new TestStatusStorage();
            await this.storage.InitAsync(storagePath, new SystemEnvironment(), optimizeForPerformance);
            Task messageProcessing = this.storage.ProcessAllMessagesAsync(message => ReportingCache.Instance.AddMessage(message));
            Task directMethodProcessing = this.storage.ProcessAllDirectMethodsAsync(directMethodStatus => ReportingCache.Instance.AddDirectMethodStatus(directMethodStatus));
            Task twinProcessing = this.storage.ProcessAllTwinsAsync(twinStatus => ReportingCache.Instance.AddTwinStatus(twinStatus));
            await Task.WhenAll(messageProcessing, directMethodProcessing, twinProcessing);
        }

        public async Task AddMessageAsync(MessageDetails msg)
        {
            bool added = await this.storage.AddMessageAsync(msg);
            if (added)
            {
                ReportingCache.Instance.AddMessage(msg);
            }
        }

        public async Task AddDirectMethodAsync(CloudOperationStatus dmStatus)
        {
            bool added = await this.storage.AddDirectMethodAsync(dmStatus);
            if (added)
            {
                ReportingCache.Instance.AddDirectMethodStatus(dmStatus);
            }
        }

        public async Task AddTwinAsync(CloudOperationStatus twinStatus)
        {
            bool added = await this.storage.AddTwinAsync(twinStatus);
            if (added)
            {
                ReportingCache.Instance.AddTwinStatus(twinStatus);
            }
        }
    }
}
