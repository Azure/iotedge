// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    [Serializable]
    public class TwinSignatureSHAException : Exception
    {
        public TwinSignatureSHAException(string message)
            : base(message)
        {
        }

        public TwinSignatureSHAException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
