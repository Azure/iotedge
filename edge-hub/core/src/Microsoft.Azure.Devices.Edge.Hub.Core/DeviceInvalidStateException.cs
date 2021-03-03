// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class DeviceInvalidStateException : Exception
    {
        public DeviceInvalidStateException()
        {
        }

        public DeviceInvalidStateException(string message)
            : base(message)
        {
        }

        public DeviceInvalidStateException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
