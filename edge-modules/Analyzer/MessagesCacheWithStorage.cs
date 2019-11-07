// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MessagesCacheWithStorage
    {
        Storage storage;

        public static MessagesCacheWithStorage Instance { get; } = new MessagesCacheWithStorage();

        MessagesCacheWithStorage()
        {
        }

        public async Task Init(string storagePath, bool optimizeForPerformance)
        {
            this.storage = new Storage();
            await this.storage.Init(storagePath, new SystemEnvironment(), optimizeForPerformance);
            await this.storage.ProcessAllMessages(details => MessagesCache.Instance.AddMessage(details));
            await this.storage.ProcessAllDirectMethods(dm => MessagesCache.Instance.AddDirectMethodStatus(dm));
        }

        public async Task AddMessage(MessageDetails msg)
        {
            bool added = await this.storage.AddMessage(msg);
            if (added)
            {
                MessagesCache.Instance.AddMessage(msg);
            }
        }

        public async Task AddDirectMethod(ResponseStatus dmStatus)
        {
            bool added = await this.storage.AddDirectMethod(dmStatus);
            if (added)
            {
                MessagesCache.Instance.AddDirectMethodStatus(dmStatus);
            }
        }

        public async Task AddTwin(ResponseStatus twinStatus)
        {
            bool added = await this.storage.AddTwin(twinStatus);
            if (added)
            {
                MessagesCache.Instance.AddTwinStatus(twinStatus);
            }
        }
    }
}
