// Copyright (c) Microsoft. All rights reserved.
namespace Relayer
{
    using Microsoft.Azure.Devices.Client;

    class MessageHandlerContext
    {
        public ModuleClient ModuleClient;
        public DuplicateMessageAuditor DuplicateMessageAuditor;

        MessageHandlerContext(ModuleClient moduleClient, DuplicateMessageAuditor duplicateMessageAuditor)
        {
            this.ModuleClient = moduleClient;
            this.DuplicateMessageAuditor = duplicateMessageAuditor;
        }
    }

    class DuplicateMessageAuditor
    {
        int messageDuplicateTolerance;
        int duplicateCounter;
        string previousSequenceNumber;

        DuplicateMessageAuditor(int messageDuplicateTolerance)
        {
            this.messageDuplicateTolerance = messageDuplicateTolerance;
            this.previousSequenceNumber = string.Empty;
            this.duplicateCounter = 0;
        }

        bool DoesMessageViolateDuplicateRules(string sequenceNumber)
        {
            if (sequenceNumber.Equals(this.previousSequenceNumber))
            {
                this.duplicateCounter += 1;
                return this.duplicateCounter == this.messageDuplicateTolerance;
            }

            this.previousSequenceNumber = sequenceNumber;
            this.duplicateCounter = 0;
            return false;
        }
    }
}
