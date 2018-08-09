// Copyright (c) Microsoft. All rights reserved.

namespace MessagesAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Reporter
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<Reporter>();

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
            Log.LogInformation($"Report for {moduleId}");

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
                    {
                        Log.LogDebug($"Ignore message for {moduleId} enqued at {msg.EnquedDateTime} because is after {endDateTime}");
                        break;
                    }

                    if (msg.SequenceNumber - 1 != prevSequenceNumber)
                    {
                        Log.LogInformation($"Missing messages for {moduleId} from {prevSequenceNumber} to {msg.SequenceNumber}");
                        long currentMissing = msg.SequenceNumber - prevSequenceNumber - 1;
                        missingCounter += currentMissing;
                        missingIntervals.Add(new MissedMessagesDetails(currentMissing, prevEnquedDateTime, msg.EnquedDateTime));
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
                return new ModuleReport(moduleId, StatusCode.OldMessages, totalMessagesCounter, $"No messages received for the past {toleranceInMilliseconds} milliseconds", lastMessageDateTime, missingIntervals);

            return missingCounter > 0 ? new ModuleReport(moduleId, StatusCode.SkippedMessages, totalMessagesCounter, $"Missing messages: {missingCounter}", lastMessageDateTime, missingIntervals)
                : new ModuleReport(moduleId, StatusCode.AllMessages, totalMessagesCounter, $"All messages received", lastMessageDateTime);
        }
    }
}
