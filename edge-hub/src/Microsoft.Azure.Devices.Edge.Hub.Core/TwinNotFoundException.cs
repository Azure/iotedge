// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class TwinNotFoundException : Exception
    {
        public TwinNotFoundException(string message)
            : this(message, null)
        { }

        public TwinNotFoundException(string message, Exception innerException)
        : base(message, innerException)
        { }
    }
}
