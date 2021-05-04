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

        public const string EdgeHubModuleId = "$edgeHub";
        public const string IotEdgeIdentityCapability = "iotEdge";
        public const string ServiceIdentityRefreshMethodName = "RefreshDeviceScopeIdentityCache";
        public const string IoTEdgeProductInfoIdentifier = "EdgeHub";

        public const long MaxMessageSize = 256 * 1024; // matches IoTHub

        public const string SecurityMessageIoTHubInterfaceId = "urn:azureiot:Security:SecurityAgent:1";

        public const string ServiceApiIdHeaderKey = "x-ms-edge-moduleId";
        public const string ClientCertificateHeaderKey = "x-ms-edge-clientcert";
        public const string DefaultApiProxyId = "IoTEdgeAPIProxy";

        public const string SchemaVersionKey = "schemaVersion";
        public static readonly Version SchemaVersion_1_0 = new Version("1.0");
        public static readonly Version SchemaVersion_1_1 = new Version("1.1");
        public static readonly Version SchemaVersion_1_2 = new Version("1.2");
        public static readonly Version SchemaVersion_1_3 = new Version("1.3");
        public static readonly Version LatestSchemaVersion = SchemaVersion_1_3;
    }
}
