// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;

    public static class SystemProperties
    {
        public const string MessageId = "messageId";
        public const string EnqueuedTime = "enqueuedTime";
        public const string To = "to";
        public const string CorrelationId = "correlationId";
        public const string UserId = "userId";
        public const string Ack = "ack";
        public const string ConnectionDeviceId = "connectionDeviceId";
        public const string ConnectionModuleId = "connectionModuleId";
        public const string InputName = "inputName";
        public const string OutputName = "outputName";
        public const string DeviceGenerationId = "connectionDeviceGenerationId";
        public const string AuthMethod = "connectionAuthMethod";
        public const string ContentType = "contentType";
        public const string ContentEncoding = "contentEncoding";
        public const string MessageSchema = "messageSchema";
        public const string LockToken = "lockToken";
        public const string DeliveryCount = "deliveryCount";
        public const string StatusCode = "statusCode";
        public const string OutboundUri = "outBoundURI";
        public const string Version = "version";
        public const string ExpiryTimeUtc = "absolute-expiry-time";
        public const string MessageType = "messageType";
        public const string EdgeMessageId = "edgeMessageId";

        private class OnTheWireSystemPropertyNames
        {
            public const string ExpiryTimeUtc = "$.exp";
            public const string CorrelationId = "$.cid";
            public const string MessageId = "$.mid";
            public const string To = "$.to";
            public const string UserId = "$.uid";
            public const string Ack = "ack";
            public const string OutputName = "$.on";
            public const string ConnectionDeviceId = "$.cdid";
            public const string ConnectionModuleId = "$.cmid";
            public const string ContentType = "$.ct";
            public const string ContentEncoding = "$.ce";
            public const string MessageSchema = "$.schema";
        }

        public static readonly Dictionary<string, string> IncomingSystemPropertiesMap = new Dictionary<string, string>
        {
            { OnTheWireSystemPropertyNames.ExpiryTimeUtc, ExpiryTimeUtc },
            { OnTheWireSystemPropertyNames.CorrelationId, CorrelationId },
            { OnTheWireSystemPropertyNames.MessageId, MessageId },
            { OnTheWireSystemPropertyNames.To, To },
            { OnTheWireSystemPropertyNames.UserId, UserId },
            { OnTheWireSystemPropertyNames.Ack, Ack },
            { OnTheWireSystemPropertyNames.OutputName, OutputName },
            { OnTheWireSystemPropertyNames.ContentType, ContentType },
            { OnTheWireSystemPropertyNames.ContentEncoding, ContentEncoding },
            { OnTheWireSystemPropertyNames.MessageSchema, MessageSchema }
        };

        public static readonly Dictionary<string, string> OutgoingSystemPropertiesMap = new Dictionary<string, string>
        {
            { MessageId, OnTheWireSystemPropertyNames.MessageId },
            { ConnectionDeviceId, OnTheWireSystemPropertyNames.ConnectionDeviceId  },
            { ConnectionModuleId, OnTheWireSystemPropertyNames.ConnectionModuleId },
            { ContentType, OnTheWireSystemPropertyNames.ContentType },
            { ContentEncoding, OnTheWireSystemPropertyNames.ContentEncoding },
            { MessageSchema, OnTheWireSystemPropertyNames.MessageSchema }
        };        
    }
}
