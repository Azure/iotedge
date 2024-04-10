// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Runtime.Serialization;

    class TestInitializationException : Exception
    {
        public TestInitializationException()
        {
        }

        public TestInitializationException(string message)
            : base(message)
        {
        }

        public TestInitializationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
