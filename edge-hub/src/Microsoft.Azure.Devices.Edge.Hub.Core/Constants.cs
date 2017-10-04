// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public class Constants
    {
        public const string TwinChangeNotificationMessageSchema = "twinChangeNotification";
        public const string TwinChangeNotificationMessageType = "twinChangeNotification";

        public const string MessageStorePartitionKey = "messages";
        public const string TwinStorePartitionKey = "twins";
        public const string CheckpointStorePartitionKey = "checkpoints";
        public const string SessionStorePartitionKey = "sessions";

        public const string ConfigSchemaVersion = "1.0";
    }
}
