// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    /// <summary>
    /// Local error codes for AMQP error mapping, replacing
    /// the removed Microsoft.Azure.Devices.Common.Exceptions.ErrorCode from SDK v1.
    /// </summary>
    public enum ErrorCode
    {
        InvalidErrorCode = 0,
        InvalidOperation,
        ArgumentInvalid,
        ArgumentNull,
        IotHubUnauthorizedAccess,
        IotHubUnauthorized,
        DeviceNotFound,
        DeviceMessageLockLost,
        IotHubQuotaExceeded,
        DeviceMaximumQueueDepthExceeded,
        IotHubMaxCbsTokenExceeded,
        IotHubSuspended,
        IotHubNotFound,
        PreconditionFailed,
        MessageTooLarge,
        ThrottlingException,
        ServerError,
    }
}
