// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;

    public static class SystemProperties
    {
        public const string Ack = "ack";
        public const string AuthMethod = "connectionAuthMethod";
        public const string ConnectionDeviceId = "connectionDeviceId";
        public const string ConnectionModuleId = "connectionModuleId";
        public const string ContentEncoding = "contentEncoding";
        public const string ContentType = "contentType";
        public const string CorrelationId = "correlationId";
        public const string CreationTime = "creationTime";
        public const string DeliveryCount = "deliveryCount";
        public const string DeviceGenerationId = "connectionDeviceGenerationId";
        public const string EdgeHubOriginInterface = "edgeHubOriginInterface";
        public const string EdgeMessageId = "edgeMessageId";
        public const string EnqueuedTime = "enqueuedTime";
        public const string ExpiryTimeUtc = "absolute-expiry-time";
        public const string InputName = "inputName";
        public const string LockToken = "lockToken";
        public const string MessageId = "messageId";
        public const string MessageSchema = "messageSchema";
        public const string MessageType = "messageType";
        public const string MsgCorrelationId = "cid";
        public const string Operation = "operation";
        public const string OutboundUri = "outBoundURI";
        public const string OutputName = "outputName";
        public const string SequenceNumber = "sequenceNumber";
        public const string StatusCode = "statusCode";
        public const string To = "to";
        public const string UserId = "userId";
        public const string Version = "version";

        public static readonly Dictionary<string, string> IncomingSystemPropertiesMap = new Dictionary<string, string>
        {
            { OnTheWireSystemPropertyNames.ExpiryTimeUtcOnTheWireName, ExpiryTimeUtc },
            { OnTheWireSystemPropertyNames.CorrelationIdOnTheWireName, MsgCorrelationId },
            { OnTheWireSystemPropertyNames.MessageIdOnTheWireName, MessageId },
            { OnTheWireSystemPropertyNames.ToOnTheWireName, To },
            { OnTheWireSystemPropertyNames.UserIdOnTheWireName, UserId },
            { OnTheWireSystemPropertyNames.AckOnTheWireName, Ack },
            { OnTheWireSystemPropertyNames.OutputNameOnTheWireName, OutputName },
            { OnTheWireSystemPropertyNames.ContentTypeOnTheWireName, ContentType },
            { OnTheWireSystemPropertyNames.ContentEncodingOnTheWireName, ContentEncoding },
            { OnTheWireSystemPropertyNames.MessageSchemaOnTheWireName, MessageSchema },
            { OnTheWireSystemPropertyNames.OperationOnTheWireName, Operation },
            { OnTheWireSystemPropertyNames.CreationTimeOnTheWireName, CreationTime }
        };

        public static readonly Dictionary<string, string> OutgoingSystemPropertiesMap = new Dictionary<string, string>
        {
            { ExpiryTimeUtc, OnTheWireSystemPropertyNames.ExpiryTimeUtcOnTheWireName },
            { MsgCorrelationId, OnTheWireSystemPropertyNames.CorrelationIdOnTheWireName },
            { MessageId, OnTheWireSystemPropertyNames.MessageIdOnTheWireName },
            { To, OnTheWireSystemPropertyNames.ToOnTheWireName },
            { UserId, OnTheWireSystemPropertyNames.UserIdOnTheWireName },
            { Ack, OnTheWireSystemPropertyNames.AckOnTheWireName },
            { ContentType, OnTheWireSystemPropertyNames.ContentTypeOnTheWireName },
            { ContentEncoding, OnTheWireSystemPropertyNames.ContentEncodingOnTheWireName },
            { MessageSchema, OnTheWireSystemPropertyNames.MessageSchemaOnTheWireName },
            { Operation, OnTheWireSystemPropertyNames.OperationOnTheWireName },
            { CreationTime, OnTheWireSystemPropertyNames.CreationTimeOnTheWireName },
            { ConnectionDeviceId, OnTheWireSystemPropertyNames.ConnectionDeviceIdOnTheWireName },
            { ConnectionModuleId, OnTheWireSystemPropertyNames.ConnectionModuleIdOnTheWireName }
        };

        static class OnTheWireSystemPropertyNames
        {
            public const string AckOnTheWireName = "ack";
            public const string ConnectionDeviceIdOnTheWireName = "$.cdid";
            public const string ConnectionModuleIdOnTheWireName = "$.cmid";
            public const string ContentEncodingOnTheWireName = "$.ce";
            public const string ContentTypeOnTheWireName = "$.ct";
            public const string CorrelationIdOnTheWireName = "$.cid";
            public const string CreationTimeOnTheWireName = "$.ctime";
            public const string ExpiryTimeUtcOnTheWireName = "$.exp";
            public const string MessageIdOnTheWireName = "$.mid";
            public const string MessageSchemaOnTheWireName = "$.schema";
            public const string OperationOnTheWireName = "iothub-operation";
            public const string OutputNameOnTheWireName = "$.on";
            public const string ToOnTheWireName = "$.to";
            public const string UserIdOnTheWireName = "$.uid";
        }
    }
}
