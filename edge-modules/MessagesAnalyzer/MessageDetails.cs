// Copyright (c) Microsoft. All rights reserved.

namespace MessagesAnalyzer
{
    using System;

    class MessageDetails
    {
        public long SequenceNumber { get; }

        public DateTime EnquedDateTime { get; }

        public MessageDetails(long seqNumber, DateTime enquedDateTime)
        {
            this.SequenceNumber = seqNumber;
            this.EnquedDateTime = enquedDateTime;
        }
    }
}
