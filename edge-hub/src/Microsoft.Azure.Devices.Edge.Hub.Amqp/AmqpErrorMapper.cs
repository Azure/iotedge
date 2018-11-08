// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Encoding;
    using Microsoft.Azure.Devices.Common.Exceptions;

    public static class AmqpErrorMapper
    {
        // Error codes
        static readonly AmqpSymbol MessageLockLostError = AmqpConstants.Vendor + ":message-lock-lost";
        static readonly AmqpSymbol IotHubNotFoundError = AmqpConstants.Vendor + ":iot-hub-not-found-error";
        static readonly AmqpSymbol ArgumentError = AmqpConstants.Vendor + ":argument-error";
        static readonly AmqpSymbol DeviceContainerThrottled = AmqpConstants.Vendor + ":device-container-throttled";
        static readonly AmqpSymbol PreconditionFailed = AmqpConstants.Vendor + ":precondition-failed";
        static readonly AmqpSymbol IotHubSuspended = AmqpConstants.Vendor + ":iot-hub-suspended";

        // Maps the ErrorCode of an IotHubException into an appropriate AMQP error code
        public static AmqpSymbol GetErrorCondition(ErrorCode errorCode)
        {
            switch (errorCode)
            {
                case ErrorCode.InvalidOperation:
                    return AmqpErrorCode.NotAllowed;

                case ErrorCode.ArgumentInvalid:
                case ErrorCode.ArgumentNull:
                    return ArgumentError;

                case ErrorCode.IotHubUnauthorizedAccess:
                case ErrorCode.IotHubUnauthorized:
                    return AmqpErrorCode.UnauthorizedAccess;

                case ErrorCode.DeviceNotFound:
                    return AmqpErrorCode.NotFound;

                case ErrorCode.DeviceMessageLockLost:
                    return MessageLockLostError;

                case ErrorCode.IotHubQuotaExceeded:
                case ErrorCode.DeviceMaximumQueueDepthExceeded:
                case ErrorCode.IotHubMaxCbsTokenExceeded:
                    return AmqpErrorCode.ResourceLimitExceeded;

                case ErrorCode.IotHubSuspended:
                    return IotHubSuspended;

                case ErrorCode.IotHubNotFound:
                    return IotHubNotFoundError;

                case ErrorCode.PreconditionFailed:
                    return PreconditionFailed;

                case ErrorCode.MessageTooLarge:
                    return AmqpErrorCode.MessageSizeExceeded;

                case ErrorCode.ThrottlingException:
                    return DeviceContainerThrottled;

                default:
                    return AmqpErrorCode.InternalError;
            }
        }
    }
}
