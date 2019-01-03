// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Amqp;

    public static class Constants
    {
        public const string AmqpsScheme = "amqps";
        public const uint DefaultAmqpConnectionIdleTimeoutInMilliSeconds = 4 * 60 * 1000;
        public const uint MinimumAmqpHeartbeatSendInterval = 5 * 1000;
        public const uint DefaultAmqpHeartbeatSendInterval = 2 * 60 * 1000;
        public static readonly AmqpVersion AmqpVersion100 = new AmqpVersion(1, 0, 0);

        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

        // NOTE: IoT Hub service has this note on this constant:
        // Temporarily accept messages upto 1Mb in size. Reduce to 256 kb after fixing client behavior
        public const ulong AmqpMaxMessageSize = 256 * 1024 * 4;

        public const string MessageAnnotationsEnqueuedTimeKey = "iothub-enqueuedtime";
        public const string MessageAnnotationsDeliveryCountKey = "iothub-deliverycount";
        public const string MessagePropertiesMessageSchemaKey = "iothub-message-schema";
        public const string MessagePropertiesCreationTimeKey = "iothub-creation-time-utc";
        public const string MessageAnnotationsLockTokenName = "x-opt-lock-token";
        public const string MessageAnnotationsSequenceNumberName = "x-opt-sequence-number";
        public const string MessagePropertiesOperationKey = "iothub-operation";
        public const string MessagePropertiesStatusKey = "IoThub-status";
        public const string MessagePropertiesOutputNameKey = "iothub-outputname";
        public const string MessagePropertiesMethodNameKey = "IoThub-methodname";
        public const string MessageAnnotationsInputNameKey = "x-opt-input-name";
        public const string MessageAnnotationsConnectionDeviceId = "iothub-connection-device-id";
        public const string MessageAnnotationsConnectionModuleId = "iothub-connection-module-id";
        public const string WebSocketSubProtocol = "AMQPWSB10";
        public const string WebSocketListenerName = WebSocketSubProtocol +"-listener";
        public const string ServiceBusCbsSaslMechanismName = "MSSBCBS";
    }
}
