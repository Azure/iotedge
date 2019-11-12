// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CacheWithStorage
    {
        Storage storage;

        public static CacheWithStorage Instance { get; } = new CacheWithStorage();

        CacheWithStorage()
        {
        }

        public async Task Init(string storagePath, bool optimizeForPerformance)
        {
            this.storage = new Storage();
            await this.storage.Init(storagePath, new SystemEnvironment(), optimizeForPerformance);
            await this.storage.ProcessAllMessages(message => Cache.Instance.AddMessage(message));
            await this.storage.ProcessAllDirectMethods(directMethodStatus => Cache.Instance.AddDirectMethodStatus(directMethodStatus));
            await this.storage.ProcessAllTwins(twinStatus => Cache.Instance.AddTwinStatus(twinStatus));
        }

        public async Task AddMessage(MessageDetails msg)
        {
            bool added = await this.storage.AddMessage(msg);
            if (added)
            {
                Cache.Instance.AddMessage(msg);
            }
        }

        public async Task AddDirectMethod(ResponseStatus dmStatus)
        {
            bool added = await this.storage.AddDirectMethod(dmStatus);
            if (added)
            {
                Cache.Instance.AddDirectMethodStatus(dmStatus);
            }
        }

        public async Task AddTwin(ResponseStatus twinStatus)
        {
            bool added = await this.storage.AddTwin(twinStatus);
            if (added)
            {
                Cache.Instance.AddTwinStatus(twinStatus);
            }
        }
    }
}
