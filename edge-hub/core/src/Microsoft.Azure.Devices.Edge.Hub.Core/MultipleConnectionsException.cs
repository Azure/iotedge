// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class MultipleConnectionsException : Exception
    {
        public MultipleConnectionsException(string message)
            : base(message)
        {
        }
    }
}
