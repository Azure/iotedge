// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public static class SystemProperties
    {
        public const string MessageId = "messageId";
        public const string EnqueuedTime = "enqueuedTime";
        public const string To = "to";
        public const string CorrelationId = "correlationId";
        public const string UserId = "userId";
        public const string Ack = "ack";
        public const string DeviceId = "connectionDeviceId";
        public const string DeviceGenerationId = "connectionDeviceGenerationId";
        public const string AuthMethod = "connectionAuthMethod";
        public const string ContentType = "contentType";
        public const string ContentEncoding = "contentEncoding";
    }
}
