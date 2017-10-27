// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;

    [Serializable]
    public class EmptyModuleSetException : Exception
    {
        public EmptyModuleSetException(string message) : base(message)
        {
        }

        public EmptyModuleSetException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
