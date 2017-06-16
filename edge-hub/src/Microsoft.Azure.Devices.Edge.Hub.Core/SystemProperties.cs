
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
        public const string DeviceId = "connectionDeviceId";
        public const string ModuleId = "moduleId";
        public const string EndpointId = "endpointId";
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
        
        public static readonly Dictionary<string, string> IncomingSystemPropertiesMap = new Dictionary<string, string>
        {
            { "$.exp", ExpiryTimeUtc },
            { "$.cid", CorrelationId },
            { "$.mid", MessageId },
            { "$.to", To },
            { "$.uid", UserId },
            { "ack", Ack },
            { "$.mop", EndpointId }
        };
    }
}
