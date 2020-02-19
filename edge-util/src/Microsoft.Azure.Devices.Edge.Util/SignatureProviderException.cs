// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    /// <summary>
    /// The exception that is thrown when a signature provider fails to sign data.
    /// </summary>
    public class SignatureProviderException : Exception
    {
        public SignatureProviderException(Exception inner)
            : base(inner.Message, inner)
        {
        }
    }
}
