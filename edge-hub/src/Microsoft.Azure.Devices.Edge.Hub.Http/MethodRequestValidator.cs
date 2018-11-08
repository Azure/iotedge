// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MethodRequestValidator : IValidator<MethodRequest>
    {
        const int DeviceMethodNameMaxLength = 100;
        const int PayloadMaxSizeBytes = 8 * 1024; // 8kb
        static readonly TimeSpan DeviceMethodMaxResponseTimeout = TimeSpan.FromMinutes(5);
        static readonly TimeSpan DeviceMethodMinResponseTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan DeviceMethodMaxDispatchTimeout = TimeSpan.FromMinutes(5);

        public void Validate(MethodRequest methodRequest)
        {
            Preconditions.CheckNotNull(methodRequest, nameof(methodRequest));

            Preconditions.CheckNonWhiteSpace(methodRequest.MethodName, "MethodName");

            if (methodRequest.MethodName.Length > DeviceMethodNameMaxLength)
            {
                throw new ArgumentException($"MethodName cannot be longer than {DeviceMethodNameMaxLength}");
            }

            if (methodRequest.ConnectTimeout > DeviceMethodMaxDispatchTimeout || methodRequest.ConnectTimeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException($"ConnectTimeout has to be between 0 and {DeviceMethodMaxDispatchTimeout.TotalSeconds} seconds.");
            }

            if (methodRequest.ResponseTimeout > DeviceMethodMaxResponseTimeout || methodRequest.ResponseTimeout < DeviceMethodMinResponseTimeout)
            {
                throw new ArgumentOutOfRangeException($"ResponseTimeout has to be between {DeviceMethodMinResponseTimeout.TotalSeconds} and {DeviceMethodMaxResponseTimeout.TotalSeconds} seconds.");
            }

            if (methodRequest.PayloadBytes != null && methodRequest.PayloadBytes.Length > PayloadMaxSizeBytes)
            {
                throw new ArgumentException($"Payload should not be greater than {PayloadMaxSizeBytes} bytes.");
            }
        }
    }
}
