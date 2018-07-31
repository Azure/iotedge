// Copyright (c) Microsoft. All rights reserved.

namespace MessagesAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class Reporter
    {
        public static DeviceReport GetReceivedMessagesReport(double toleranceInMilliseconds)
        {
            DateTime enDateTime = DateTime.UtcNow;
            IDictionary<string, IList<SortedSet<MessageDetails>>> messages = MessagesCache.Instance.GetMessagesSnapshot();
            IList<ModuleReport> report = new List<ModuleReport>();

            foreach (KeyValuePair<string, IList<SortedSet<MessageDetails>>> moduleMessages in messages)
            {
                report.Add(GetReceivedMessagesReport(moduleMessages.Key, toleranceInMilliseconds, moduleMessages.Value, enDateTime));
            }

            return new DeviceReport(report);
        }

        static ModuleReport GetReceivedMessagesReport(string moduleId, double toleranceInMilliseconds, IList<SortedSet<MessageDetails>> batchesSnapshot, DateTime endDateTime)
        {
            long missingCounter = 0;
            long totalMessagesCounter = 0;
            IList<MissedMessagesDetails> missingIntervals = new List<MissedMessagesDetails>();

            if (batchesSnapshot.Count == 0)
            {
                return new ModuleReport(moduleId, StatusCode.NoMessages, totalMessagesCounter, "No messages received for module");
            }

            DateTime lastMessageDateTime = DateTime.MinValue;

            foreach (SortedSet<MessageDetails> messageDetails in batchesSnapshot)
            {
                long prevSequenceNumber = messageDetails.First().SequenceNumber;
                DateTime prevEnquedDateTime = messageDetails.First().EnquedDateTime;

                foreach (MessageDetails msg in messageDetails.Skip(1))
                {
                    // ignore messages enqued after endTime
                    if (DateTime.Compare(endDateTime, msg.EnquedDateTime) < 0)
                        break;

                    if (msg.SequenceNumber - 1 != prevSequenceNumber)
                    {
                        missingCounter += msg.SequenceNumber - prevSequenceNumber;
                        missingIntervals.Add(new MissedMessagesDetails(msg.SequenceNumber - prevSequenceNumber, prevEnquedDateTime, msg.EnquedDateTime));
                    }
                    else
                    {
                        totalMessagesCounter++;
                    }

                    prevSequenceNumber = msg.SequenceNumber;
                    prevEnquedDateTime = msg.EnquedDateTime;
                    lastMessageDateTime = lastMessageDateTime < msg.EnquedDateTime ? msg.EnquedDateTime : lastMessageDateTime;
                }
            }

            //check if last message is older
            if (DateTime.Compare(lastMessageDateTime.AddMilliseconds(toleranceInMilliseconds), endDateTime) < 0)
                return new ModuleReport(moduleId, StatusCode.OldMessages, totalMessagesCounter, $"No messages received for the past {toleranceInMilliseconds} milliseconds", lastMessageDateTime);

            return missingCounter > 0 ? new ModuleReport(moduleId, StatusCode.SkippedMessages, totalMessagesCounter, $"Missing messages: {missingCounter}", lastMessageDateTime, missingIntervals)
                : new ModuleReport(moduleId, StatusCode.AllMessages, totalMessagesCounter, $"All messages received", lastMessageDateTime);
        }
    }
}
