// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class Constants
    {
        public const string TwinChangeNotificationMessageSchema = "twinChangeNotification";
        public const string TwinChangeNotificationMessageType = "twinChangeNotification";

        public const string MessageStorePartitionKey = "messages";
        public const string TwinStorePartitionKey = "twins";
        public const string CheckpointStorePartitionKey = "checkpoints";
        public const string SessionStorePartitionKey = "sessions";

        public const string InternalOriginInterface = "internal";
        public const string DownstreamOriginInterface = "downstream";

        public const string IotEdgeIdentityCapability = "iotEdge";
        public const string ServiceIdentityRefreshMethodName = "RefreshDeviceScopeIdentityCache";
        public static readonly Version ConfigSchemaVersion = new Version("1.0");
    }
}
