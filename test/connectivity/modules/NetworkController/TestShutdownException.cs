// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Runtime.Serialization;

    class TestShutdownException : Exception
    {
        public TestShutdownException()
        {
        }

        public TestShutdownException(string message)
            : base(message)
        {
        }

        public TestShutdownException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
