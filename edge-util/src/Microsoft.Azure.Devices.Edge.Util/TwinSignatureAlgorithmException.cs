// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    [Serializable]
    public class TwinSignatureAlgorithmException : Exception
    {
        public TwinSignatureAlgorithmException(string message)
            : base(message)
        {
        }

        public TwinSignatureAlgorithmException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
