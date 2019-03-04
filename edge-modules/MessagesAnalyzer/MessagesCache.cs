// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    class MessagesCache
    {
        // maps batchId with moduleId, there can be multiple batches for a module
        readonly ConcurrentDictionary<string, string> batches = new ConcurrentDictionary<string, string>();

        // maps batchId with messages
        readonly ConcurrentDictionary<string, IList<MessageDetails>> messages = new ConcurrentDictionary<string, IList<MessageDetails>>();
        readonly IComparer<MessageDetails> comparer = new EventDataComparer();

        MessagesCache()
        {
        }

        public static MessagesCache Instance { get; } = new MessagesCache();

        public void AddMessage(string moduleId, string batchId, MessageDetails messageDetails)
        {
            this.batches.TryAdd(batchId, moduleId);

            IList<MessageDetails> batchMessages = this.messages.GetOrAdd(batchId, key => new List<MessageDetails>());
            this.AddMessageDetails(batchMessages, messageDetails);
        }

        public IDictionary<string, IList<SortedSet<MessageDetails>>> GetMessagesSnapshot()
        {
            IDictionary<string, IList<SortedSet<MessageDetails>>> snapshotResult = new Dictionary<string, IList<SortedSet<MessageDetails>>>();

            IDictionary<string, string> batchesSnapshot = this.batches.ToArray().ToDictionary(p => p.Key, p => p.Value);
            IDictionary<string, IList<MessageDetails>> messagesSnapshot = this.messages.ToArray().ToDictionary(p => p.Key, p => p.Value);

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
                    var batchSortedMessages = new List<SortedSet<MessageDetails>>
                    {
                        new SortedSet<MessageDetails>(detailsSnapshot, this.comparer)
                    };
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
