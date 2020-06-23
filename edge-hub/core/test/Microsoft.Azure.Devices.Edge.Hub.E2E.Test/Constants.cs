// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    public static class Constants
    {
        public static class TestPriority
        {
            public static class StressTest
            {
                public const int SingleSenderSingleReceiverTest = 301;
                public const int MultipleSendersSingleReceiverTest = 302;
                public const int MultipleSendersMultipleReceivers_Count_Test = 303;
                public const int MultipleSendersMultipleReceivers_Duration_Test = 304;
                public const int BackupAndRestoreLargeBackupSizeTest = 305;
            }
        }
    }
}
