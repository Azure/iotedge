// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class Constants
    {
        public const string CheckpointStorePartitionKey = "checkpoints";
        public const string DownstreamOriginInterface = "downstream";
        public const string InternalOriginInterface = "internal";
        public const string IotEdgeIdentityCapability = "iotEdge";
        public const string MessageStorePartitionKey = "messages";
        public const string ServiceIdentityRefreshMethodName = "RefreshDeviceScopeIdentityCache";
        public const string SessionStorePartitionKey = "sessions";
        public const string TwinChangeNotificationMessageSchema = "twinChangeNotification";
        public const string TwinChangeNotificationMessageType = "twinChangeNotification";
        public const string TwinStorePartitionKey = "twins";
        public static readonly Version ConfigSchemaVersion = new Version("1.0");
    }
}
