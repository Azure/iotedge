// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
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

        protected TestShutdownException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
