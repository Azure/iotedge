// Copyright (c) Microsoft. All rights reserved.
namespace Relayer
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;

    class MessageHandlerContext
    {
        public ModuleClient ModuleClient;
        public DuplicateMessageAuditor DuplicateMessageAuditor;

        public MessageHandlerContext(ModuleClient moduleClient, DuplicateMessageAuditor duplicateMessageAuditor)
        {
            this.ModuleClient = moduleClient;
            this.DuplicateMessageAuditor = duplicateMessageAuditor;
        }
    }

    // Sometimes EdgeHub itself will duplicate a message when sending to this module.
    // When this happens, we don't want to report two expected results to the TRC, as
    // the TRC intentionally will fail duplicate expected results. Therefore we should
    // filter duplicate messages from edgehub up to some tolerance period. If this
    // tolerance for duplicates is exceeded, this module will not filter the proceeding
    // duplicate messages and will report duplicate expected results to the TRC, which
    // will fail the tests. That way we will know something is wrong.
    //
    // Below is how the duplicate filtering will work regarding different incoming messages.
    //
    // Case 1: New sequence number
    //         This cannot be a duplicate, so we don't filter
    // Case 2: Repeating sequence number under duplicate threshold
    //         We expect edgehub to sometimes send duplicates, so it is ok.
    // Case 1: Repeating sequence number over duplicate threshold
    //         Something weird is likely going on, so we won't filter in order to fail the tests
    class DuplicateMessageAuditor
    {
        int messageDuplicateTolerance;
        int duplicateCounter;
        string previousSequenceNumber;
        static readonly ILogger Logger = ModuleUtil.CreateLogger("Relayer");

        public DuplicateMessageAuditor(int messageDuplicateTolerance)
        {
            this.messageDuplicateTolerance = messageDuplicateTolerance;
            this.previousSequenceNumber = string.Empty;
            this.duplicateCounter = 0;
        }

        public bool ShouldFilterMessage(string sequenceNumber)
        {
            if (sequenceNumber.Equals(this.previousSequenceNumber))
            {
                this.duplicateCounter += 1;
                if (this.duplicateCounter >= this.messageDuplicateTolerance)
                {
                    Logger.LogError($"Message with duplicate sequence number exceeded message duplicate tolerance: sequenceNumber={sequenceNumber}, duplicateThreshold={this.messageDuplicateTolerance}");
                    return false;
                }
                else
                {
                    Logger.LogWarning($"Received message with duplicate sequence number within message duplicate tolerance: sequenceNumber={sequenceNumber}, duplicateThreshold={this.messageDuplicateTolerance}");
                    return true;
                }
            }

            this.previousSequenceNumber = sequenceNumber;
            this.duplicateCounter = 0;
            return false;
        }
    }
}
