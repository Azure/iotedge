// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    class ReportingCache
    {
        // maps batchId with moduleId, there can be multiple batches for a module
        readonly ConcurrentDictionary<string, string> batches = new ConcurrentDictionary<string, string>();
        // maps batchId with messages
        readonly ConcurrentDictionary<string, IList<MessageDetails>> messagesReportCache = new ConcurrentDictionary<string, IList<MessageDetails>>();
        // maps module id with a dictionary of status code counts
        readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Tuple<int, DateTime>>> directMethodsReportCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, Tuple<int, DateTime>>>();
        // maps module id with a dictionary of status code counts
        readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Tuple<int, DateTime>>> twinsReportCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, Tuple<int, DateTime>>>();
        readonly IComparer<MessageDetails> comparer = new EventDataComparer();

        ReportingCache()
        {
        }

        public static ReportingCache Instance { get; } = new ReportingCache();

        public void AddDirectMethodStatus(CloudOperationStatus directMethodStatus)
        {
            this.AddStatus(directMethodStatus, this.directMethodsReportCache);
        }

        public void AddTwinStatus(CloudOperationStatus twinStatus)
        {
            this.AddStatus(twinStatus, this.twinsReportCache);
        }

        public void AddStatus(CloudOperationStatus responseStatus, ConcurrentDictionary<string, ConcurrentDictionary<string, Tuple<int, DateTime>>> cache)
        {
            // lock needed for update of concurrent dict
            lock (cache)
            {
                ConcurrentDictionary<string, Tuple<int, DateTime>> batch = cache.GetOrAdd(responseStatus.ModuleId, key => new ConcurrentDictionary<string, Tuple<int, DateTime>>());

                batch.AddOrUpdate(
                    responseStatus.StatusCode,
                    new Tuple<int, DateTime>(1, responseStatus.ResponseDateTime),
                    (key, value) => new Tuple<int, DateTime>(
                        value.Item1 + 1,
                        responseStatus.ResponseDateTime > value.Item2 ? responseStatus.ResponseDateTime : value.Item2));
            }
        }

        public void AddMessage(MessageDetails messageDetails)
        {
            this.batches.TryAdd(messageDetails.BatchId, messageDetails.ModuleId);

            IList<MessageDetails> batchMessages = this.messagesReportCache.GetOrAdd(messageDetails.BatchId, key => new List<MessageDetails>());
            this.AddMessageDetails(batchMessages, messageDetails);
        }

        public IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> GetDirectMethodsSnapshot()
        {
            return this.GetSnapshotHelper(this.directMethodsReportCache);
        }

        public IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> GetTwinsSnapshot()
        {
            return this.GetSnapshotHelper(this.twinsReportCache);
        }

        public IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> GetSnapshotHelper(ConcurrentDictionary<string, ConcurrentDictionary<string, Tuple<int, DateTime>>> cache)
        {
            return cache.ToArray().ToDictionary(p => p.Key, p => (IDictionary<string, Tuple<int, DateTime>>)p.Value.ToArray().ToDictionary(t => t.Key, t => t.Value));
        }

        public IDictionary<string, IList<SortedSet<MessageDetails>>> GetMessagesSnapshot()
        {
            IDictionary<string, IList<SortedSet<MessageDetails>>> snapshotResult = new Dictionary<string, IList<SortedSet<MessageDetails>>>();

            IDictionary<string, string> batchesSnapshot = this.batches.ToArray().ToDictionary(p => p.Key, p => p.Value);
            IDictionary<string, IList<MessageDetails>> messagesSnapshot = this.messagesReportCache.ToArray().ToDictionary(p => p.Key, p => p.Value);

            foreach (KeyValuePair<string, IList<MessageDetails>> batchMessages in messagesSnapshot)
            {
                IList<MessageDetails> detailsSnapshot = this.GetMessageDetailsSnapshot(batchMessages.Value);
                string moduleId = batchesSnapshot[batchMessages.Key];

                if (snapshotResult.TryGetValue(moduleId, out IList<SortedSet<MessageDetails>> msg))
                {
                    msg.Add(new SortedSet<MessageDetails>(detailsSnapshot, this.comparer));
                }
                else
                {
                    var batchSortedMessages = new List<SortedSet<MessageDetails>>();

                    var batch = new SortedSet<MessageDetails>(this.comparer);
                    foreach (MessageDetails messageDetails in detailsSnapshot)
                    {
                        if (!batch.Add(messageDetails))
                        {
                            Console.WriteLine($"Duplicate {messageDetails.EnqueuedDateTime} {messageDetails.SequenceNumber}");
                        }
                    }

                    batchSortedMessages.Add(batch);
                    snapshotResult.Add(moduleId, batchSortedMessages);
                }
            }

            return snapshotResult;
        }

        void AddMessageDetails(IList<MessageDetails> batchMessages, MessageDetails messageDetails)
        {
            lock (batchMessages)
            {
                batchMessages.Add(messageDetails);
            }
        }

        IList<MessageDetails> GetMessageDetailsSnapshot(IList<MessageDetails> batchMessages)
        {
            MessageDetails[] details;
            lock (batchMessages)
            {
                details = new MessageDetails[batchMessages.Count];
                batchMessages.CopyTo(details, 0);
            }

            return details;
        }

        class EventDataComparer : IComparer<MessageDetails>
        {
            public int Compare(MessageDetails msg1, MessageDetails msg2)
            {
                if (msg1 == null)
                {
                    return -1;
                }

                if (msg2 == null)
                {
                    return 1;
                }

                return msg1.SequenceNumber.CompareTo(msg2.SequenceNumber);
            }
        }
    }
}
