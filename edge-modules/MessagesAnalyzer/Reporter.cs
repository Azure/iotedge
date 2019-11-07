// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;

    class Reporter
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("Analyzer");

        public static DeviceAnalysis GetDeviceReport(double toleranceInMilliseconds)
        {
            return new DeviceAnalysis(GetReceivedMessagesReport(toleranceInMilliseconds), GetDirectMethodsReport(), GetTwinsReport());
        }

        static IList<ResponseOrientedReport> GetDirectMethodsReport()
        {
            IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> dms = MessagesCache.Instance.GetDirectMethodsSnapshot();
            string description = "Report for direct methods";
            return GetReportHelper(dms, description);
        }

        static IList<ResponseOrientedReport> GetTwinsReport()
        {
            IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> twins = MessagesCache.Instance.GetTwinsSnapshot();
            string description = "Report for twins";
            return GetReportHelper(twins, description);
        }

        static IList<ResponseOrientedReport> GetReportHelper(IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> cache, string reportDescription)
        {
            IList<ResponseOrientedReport> report = new List<ResponseOrientedReport>();

            foreach (KeyValuePair<string, IDictionary<string, Tuple<int, DateTime>>> obj in cache)
            {
                // TODO: specify report type in logs through enum of response oriented report types
                Logger.LogInformation($"{reportDescription} {obj.Key}");
                report.Add(new ResponseOrientedReport(obj.Key, obj.Value));
            }

            return report;
        }

        static IList<ModuleMessagesReport> GetReceivedMessagesReport(double toleranceInMilliseconds)
        {
            DateTime enDateTime = DateTime.UtcNow;
            IDictionary<string, IList<SortedSet<MessageDetails>>> messages = MessagesCache.Instance.GetMessagesSnapshot();
            IList<ModuleMessagesReport> report = new List<ModuleMessagesReport>();

            foreach (KeyValuePair<string, IList<SortedSet<MessageDetails>>> moduleMessages in messages)
            {
                report.Add(GetReceivedMessagesReport(moduleMessages.Key, toleranceInMilliseconds, moduleMessages.Value, enDateTime));
            }

            return report;
        }

        static ModuleMessagesReport GetReceivedMessagesReport(string moduleId, double toleranceInMilliseconds, IList<SortedSet<MessageDetails>> batchesSnapshot, DateTime endDateTime)
        {
            Logger.LogInformation($"Messages report for {moduleId}");

            long missingCounter = 0;
            long totalMessagesCounter = 0;
            IList<MissedMessagesDetails> missedMessages = new List<MissedMessagesDetails>();

            if (batchesSnapshot.Count == 0)
            {
                return new ModuleMessagesReport(moduleId, StatusCode.NoMessages, totalMessagesCounter, "No messages received for module");
            }

            DateTime lastMessageDateTime = DateTime.MinValue;

            foreach (SortedSet<MessageDetails> messageDetails in batchesSnapshot)
            {
                long prevSequenceNumber = messageDetails.First().SequenceNumber;
                DateTime prevEnquedDateTime = messageDetails.First().EnqueuedDateTime;

                foreach (MessageDetails msg in messageDetails.Skip(1))
                {
                    // ignore messages enqued after endTime
                    if (DateTime.Compare(endDateTime, msg.EnqueuedDateTime) < 0)
                    {
                        Logger.LogDebug($"Ignore message for {moduleId} enqued at {msg.EnqueuedDateTime} because is after {endDateTime}");
                        break;
                    }

                    if (msg.SequenceNumber - 1 != prevSequenceNumber)
                    {
                        Logger.LogInformation($"Missing messages for {moduleId} from {prevSequenceNumber} to {msg.SequenceNumber} exclusive.");
                        long currentMissing = msg.SequenceNumber - prevSequenceNumber - 1;
                        missingCounter += currentMissing;
                        missedMessages.Add(new MissedMessagesDetails(currentMissing, prevEnquedDateTime, msg.EnqueuedDateTime));
                    }

                    totalMessagesCounter++;
                    prevSequenceNumber = msg.SequenceNumber;
                    prevEnquedDateTime = msg.EnqueuedDateTime;
                    lastMessageDateTime = lastMessageDateTime < msg.EnqueuedDateTime ? msg.EnqueuedDateTime : lastMessageDateTime;
                }
            }

            // check if last message is older
            if (DateTime.Compare(lastMessageDateTime.AddMilliseconds(toleranceInMilliseconds), endDateTime) < 0)
            {
                Logger.LogInformation($"Module {moduleId}: last message datetime={lastMessageDateTime} and end datetime={endDateTime}");
                return new ModuleMessagesReport(moduleId, StatusCode.OldMessages, totalMessagesCounter, $"Missing messages: {missingCounter}. No messages received for the past {toleranceInMilliseconds} milliseconds.", lastMessageDateTime, missedMessages);
            }

            return missingCounter > 0
                ? new ModuleMessagesReport(moduleId, StatusCode.SkippedMessages, totalMessagesCounter, $"Missing messages: {missingCounter}.", lastMessageDateTime, missedMessages)
                : new ModuleMessagesReport(moduleId, StatusCode.AllMessages, totalMessagesCounter, "All messages received.", lastMessageDateTime);
        }
    }
}
