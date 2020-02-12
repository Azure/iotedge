// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    /// <summary>
    /// The exception that is thrown when a token provider fails to get a token.
    /// </summary>
    public class TokenProviderException : Exception
    {
        public TokenProviderException(Exception inner)
            : base(inner.Message, inner)
        {
        }
    }
}
