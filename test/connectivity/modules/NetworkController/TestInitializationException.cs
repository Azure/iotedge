// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
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

        protected TestInitializationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
