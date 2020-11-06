// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    [Serializable]
    public class TwinSignatureAlgorithmSchemeException : Exception
    {
        public TwinSignatureAlgorithmSchemeException(string message)
            : base(message)
        {
        }

        public TwinSignatureAlgorithmSchemeException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
