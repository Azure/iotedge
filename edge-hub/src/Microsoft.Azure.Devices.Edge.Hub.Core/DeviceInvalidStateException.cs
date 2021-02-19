// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Runtime.Serialization;

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

        protected DeviceInvalidStateException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
