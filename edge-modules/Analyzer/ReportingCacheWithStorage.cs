// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ReportingCacheWithStorage
    {
        Storage storage;

        public static ReportingCacheWithStorage Instance { get; } = new ReportingCacheWithStorage();

        ReportingCacheWithStorage()
        {
        }

        public async Task Init(string storagePath, bool optimizeForPerformance)
        {
            this.storage = new Storage();
            await this.storage.Init(storagePath, new SystemEnvironment(), optimizeForPerformance);
            await this.storage.ProcessAllMessages(message => ReportingCache.Instance.AddMessage(message));
            await this.storage.ProcessAllDirectMethods(directMethodStatus => ReportingCache.Instance.AddDirectMethodStatus(directMethodStatus));
            await this.storage.ProcessAllTwins(twinStatus => ReportingCache.Instance.AddTwinStatus(twinStatus));
        }

        public async Task AddMessage(MessageDetails msg)
        {
            bool added = await this.storage.AddMessage(msg);
            if (added)
            {
                ReportingCache.Instance.AddMessage(msg);
            }
        }

        public async Task AddDirectMethod(ResponseStatus dmStatus)
        {
            bool added = await this.storage.AddDirectMethod(dmStatus);
            if (added)
            {
                ReportingCache.Instance.AddDirectMethodStatus(dmStatus);
            }
        }

        public async Task AddTwin(ResponseStatus twinStatus)
        {
            bool added = await this.storage.AddTwin(twinStatus);
            if (added)
            {
                ReportingCache.Instance.AddTwinStatus(twinStatus);
            }
        }
    }
}
