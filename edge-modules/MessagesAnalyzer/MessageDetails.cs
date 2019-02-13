// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System;

    class MessageDetails
    {
        public MessageDetails(long seqNumber, DateTime enqueuedDateTime)
        {
            this.SequenceNumber = seqNumber;
            this.EnqueuedDateTime = enqueuedDateTime;
        }

        public long SequenceNumber { get; }

        public DateTime EnqueuedDateTime { get; }
    }
}
