// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;

    class TestResultReporter
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TestResultReporter));

        public static TestResultAnalysis GetDeviceReport(double toleranceInMilliseconds)
        {
            return new TestResultAnalysis(GetReceivedMessagesReport(toleranceInMilliseconds), GetDirectMethodsReport(), GetTwinsReport());
        }

        static IList<AggregateCloudOperationReport> GetDirectMethodsReport()
        {
            IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> dms = ReportingCache.Instance.GetDirectMethodsSnapshot();
            string description = "Report for direct methods";
            return GetReportHelper(dms, description);
        }

        static IList<AggregateCloudOperationReport> GetTwinsReport()
        {
            IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> twins = ReportingCache.Instance.GetTwinsSnapshot();
            string description = "Report for twins";
            return GetReportHelper(twins, description);
        }

        static IList<AggregateCloudOperationReport> GetReportHelper(IDictionary<string, IDictionary<string, Tuple<int, DateTime>>> cache, string reportDescription)
        {
            IList<AggregateCloudOperationReport> report = new List<AggregateCloudOperationReport>();

            foreach (KeyValuePair<string, IDictionary<string, Tuple<int, DateTime>>> obj in cache)
            {
                Logger.LogInformation($"{reportDescription} {obj.Key}");
                report.Add(new AggregateCloudOperationReport(obj.Key, obj.Value, Settings.Current.TestInfo));
            }

            return report;
        }

        static IList<ModuleMessagesReport> GetReceivedMessagesReport(double toleranceInMilliseconds)
        {
            DateTime endDateTime = DateTime.UtcNow;
            IDictionary<string, IList<SortedSet<MessageDetails>>> messages = ReportingCache.Instance.GetMessagesSnapshot();
            IList<ModuleMessagesReport> report = new List<ModuleMessagesReport>();

            foreach (KeyValuePair<string, IList<SortedSet<MessageDetails>>> moduleMessages in messages)
            {
                report.Add(GetReceivedMessagesReport(moduleMessages.Key, toleranceInMilliseconds, moduleMessages.Value, endDateTime));
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
                return new ModuleMessagesReport(moduleId, StatusCode.NoMessages, totalMessagesCounter, "No messages received for module", Settings.Current.TestInfo);
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

            bool areMessagesMissing = missingCounter > 0;
            bool isLastMessageTooOld = DateTime.Compare(lastMessageDateTime.AddMilliseconds(toleranceInMilliseconds), endDateTime) < 0;

            if (areMessagesMissing)
            {
                Logger.LogInformation($"Module {moduleId}: missing messages");
            }

            if (isLastMessageTooOld)
            {
                Logger.LogInformation($"Module {moduleId}: last message datetime={lastMessageDateTime} and end datetime={endDateTime}");
            }

            (StatusCode statusCode, string statusMessage) = GetStatus(areMessagesMissing, isLastMessageTooOld, missingCounter, toleranceInMilliseconds);

            return new ModuleMessagesReport(moduleId, statusCode, totalMessagesCounter, statusMessage, lastMessageDateTime, missedMessages, Settings.Current.TestInfo);
        }

        static (StatusCode, string) GetStatus(bool areMessagesMissing, bool isLastMessageTooOld, long missingCounter, double toleranceInMilliseconds)
        {
            string missingMessagesStatus = $"Missing messages: {missingCounter}.";
            string messagesTooOldStatus = $"No messages received for the past {toleranceInMilliseconds} milliseconds.";
            string noMissingMessagesStatus = "All messages received. ";

            StatusCode statusCode;
            string statusMessage;
            if (areMessagesMissing && isLastMessageTooOld)
            {
                statusCode = StatusCode.SkippedAndOldMessages;
                statusMessage = $"{missingMessagesStatus} {messagesTooOldStatus}";
            }
            else if (areMessagesMissing)
            {
                statusCode = StatusCode.SkippedMessages;
                statusMessage = $"{missingMessagesStatus}";
            }
            else if (isLastMessageTooOld)
            {
                statusCode = StatusCode.OldMessages;
                statusMessage = $"{messagesTooOldStatus} {noMissingMessagesStatus}";
            }
            else
            {
                statusCode = StatusCode.AllMessages;
                statusMessage = $"{noMissingMessagesStatus}";
            }

            return (statusCode, statusMessage);
        }
    }
}
